using System;
using System.Collections.Generic;
using CS2MPMod.Core.Diagnostics;
using CS2MPMod.Core.Networking;
using CS2MPMod.Core.Protocol.Messages;

namespace CS2MPMod.Core.Session
{
    public sealed partial class MultiplayerSession
    {
        private const int CommandJournalCapacity = 4096;
        private const long CommandJournalByteLimit = 16L * 1024 * 1024;
        private const int PendingCommandCapacity = 2048;
        private const long PendingCommandByteLimit = 8L * 1024 * 1024;
        private const int ReplayBatchSize = 256;
        private const long ReplayRetryMs = 1000;
        private const int ReplayMaxAttempts = 5;
        private const long CommandRecoveryRetryMs = 30000;

        // Sequence remains session-monotonic. Resume rebases clients to the snapshot cut;
        // epoch prevents replay of pre-snapshot work into the newly installed world.
        private long _nextCommandSequence;
        private long _commandEpoch;
        private long _lastWorldSyncEpoch;
        private long _lastReceivedCommandSequence;
        private long _journalBytes;
        private long _pendingCommandBytes;
        private long _commandNowMs;
        private long _lastReplayRequestFrom;
        private long _lastReplayRequestTo;
        private long _lastReplaySentMs;
        private int _replayAttempts;
        private bool _commandRecoveryPending;
        private long _nextCommandRecoveryMs;
        private long _commandRecoveryStartedMs;
        /// <summary>Gameplay must stand down until an authoritative snapshot repairs a lost stream.</summary>
        public bool CommandRecoveryPending => _commandRecoveryPending;
        private readonly LinkedList<SimulationCommandMessage> _commandJournal =
            new LinkedList<SimulationCommandMessage>();
        private readonly SortedDictionary<long, SimulationCommandMessage> _pendingCommands =
            new SortedDictionary<long, SimulationCommandMessage>();

        private static long CommandBytes(SimulationCommandMessage command) =>
            64L + (command.Body != null ? command.Body.Length : 0);

        private void ClearReplayRequest()
        {
            _lastReplayRequestFrom = 0;
            _lastReplayRequestTo = 0;
            _lastReplaySentMs = 0;
            _replayAttempts = 0;
        }

        private void ResetCommandSequenceState()
        {
            _nextCommandSequence = 0;
            _commandEpoch = 0;
            _lastWorldSyncEpoch = 0;
            _commandNowMs = 0;
            InstallCommandBaseline(0, 0);
        }

        private void InstallCommandBaseline(long epoch, long sequence)
        {
            _commandEpoch = epoch;
            _lastReceivedCommandSequence = sequence;
            _commandJournal.Clear();
            _journalBytes = 0;
            _pendingCommands.Clear();
            _pendingCommandBytes = 0;
            _commandRecoveryPending = false;
            _nextCommandRecoveryMs = 0;
            _commandRecoveryStartedMs = 0;
            ClearReplayRequest();
        }

        internal long NextCommandSequence() => Role == SessionRole.Host ? ++_nextCommandSequence : 0;

        internal void JournalCommand(SimulationCommandMessage command)
        {
            if (Role != SessionRole.Host || command == null || command.Sequence <= 0) return;
            // Keep an immutable replay copy; neither a caller's reusable buffer nor an
            // observer can change already recorded history.
            var copy = new SimulationCommandMessage(command.OriginPlayerId, command.Tick,
                command.Sequence, command.CommandId,
                command.Body == null ? Array.Empty<byte>() : (byte[])command.Body.Clone(), command.Epoch);
            _commandJournal.AddLast(copy);
            _journalBytes += CommandBytes(copy);
            while (_commandJournal.Count > CommandJournalCapacity || _journalBytes > CommandJournalByteLimit)
            {
                _journalBytes -= CommandBytes(_commandJournal.First.Value);
                _commandJournal.RemoveFirst();
            }
        }

        private void ReceiveOrderedCommand(SimulationCommandMessage command)
        {
            if (_commandRecoveryPending || command.Sequence <= _lastReceivedCommandSequence) return;
            long previous = _lastReceivedCommandSequence;
            if (command.Sequence == _lastReceivedCommandSequence + 1)
            {
                // Normal reliable delivery needs no tree node allocation. This also lets
                // the missing predecessor release a buffer that is exactly at its cap.
                DispatchOrderedCommand(command);
            }
            else
            {
                if (_pendingCommands.ContainsKey(command.Sequence)) return;
                long bytes = CommandBytes(command);
                if (_pendingCommands.Count >= PendingCommandCapacity ||
                    _pendingCommandBytes + bytes > PendingCommandByteLimit)
                {
                    RequestCommandRecovery("command reorder buffer exceeded its bounded capacity");
                    return;
                }
                _pendingCommands.Add(command.Sequence, command);
                _pendingCommandBytes += bytes;
            }
            while (_pendingCommands.TryGetValue(_lastReceivedCommandSequence + 1,
                                               out SimulationCommandMessage next))
            {
                _pendingCommands.Remove(next.Sequence);
                _pendingCommandBytes -= CommandBytes(next);
                DispatchOrderedCommand(next);
            }
            if (_lastReplayRequestTo > 0 && _lastReceivedCommandSequence >= _lastReplayRequestTo)
                ClearReplayRequest();
            else if (_lastReceivedCommandSequence > previous && _lastReplayRequestTo > 0)
            {
                // Slow but progressing transfers are not stalled. Don't repeatedly enqueue
                // the same large batch while its prefix is still arriving.
                _lastReplaySentMs = _commandNowMs;
                _replayAttempts = 1;
            }
            PumpCommandReplay(_commandNowMs);
        }

        private void DispatchOrderedCommand(SimulationCommandMessage command)
        {
            _lastReceivedCommandSequence = command.Sequence;
            // The sender already performed this edit locally. Consume its sequence
            // acknowledgement, but do not replay that edit into game observers.
            if (command.OriginPlayerId != LocalPlayerId) NotifyCommand(command);
        }

        private void PumpCommandReplay(long nowMs)
        {
            if (Role != SessionRole.Client || Status != SessionStatus.Connected || _worldSyncSuspended) return;
            if (_commandRecoveryPending)
            {
                if (nowMs - _commandRecoveryStartedMs >= 120000)
                {
                    Fault("Command recovery timed out: the host did not begin a replacement snapshot.");
                    return;
                }
                if (nowMs >= _nextCommandRecoveryMs)
                {
                    _nextCommandRecoveryMs = nowMs + CommandRecoveryRetryMs;
                    RequestWorldSync("command stream could not be recovered from retained history");
                }
                return;
            }
            if (_pendingCommands.Count == 0) return;
            if (_lastReplayRequestTo == 0)
            {
                long first = 0;
                foreach (var pair in _pendingCommands) { first = pair.Key; break; }
                _lastReplayRequestFrom = _lastReceivedCommandSequence + 1;
                _lastReplayRequestTo = _lastReplayRequestFrom +
                    Math.Min(ReplayBatchSize - 1L, first - _lastReplayRequestFrom - 1);
            }
            if (_replayAttempts > 0 && nowMs - _lastReplaySentMs < ReplayRetryMs) return;
            if (_replayAttempts >= ReplayMaxAttempts)
            {
                RequestCommandRecovery("command replay made no complete progress after bounded retries");
                return;
            }
            _replayAttempts++;
            _lastReplaySentMs = nowMs;
            SendTo(ConnectionId.Server, new CommandReplayRequestMessage(
                _lastReplayRequestFrom, _lastReplayRequestTo, _commandEpoch));
        }

        private void RequestCommandRecovery(string reason)
        {
            if (_commandRecoveryPending) return;
            _commandRecoveryPending = true;
            _commandRecoveryStartedMs = _commandNowMs;
            _pendingCommands.Clear();
            _pendingCommandBytes = 0;
            ClearReplayRequest();
            _nextCommandRecoveryMs = _commandNowMs;
            _log.Warn(LogTopic.Session, "Command stream paused pending a fresh snapshot: " + reason + ".");
            PumpCommandReplay(_commandNowMs);
        }

        private void HandleCommandReplayRequest(ConnectionId from, Peer peer,
            CommandReplayRequestMessage request)
        {
            if (Role != SessionRole.Host || peer == null || !peer.Handshaked) return;
            if (request.Epoch < 0 || request.FromSequence <= 0 || request.ToSequence < request.FromSequence ||
                request.ToSequence == long.MaxValue || request.ToSequence - request.FromSequence >= ReplayBatchSize)
            {
                Punt(from, peer, "invalid command replay range", "CommandReplayRequest");
                return;
            }
            if (_worldSyncSuspended) return; // the in-flight snapshot will supersede replay

            // Validate the ENTIRE range before sending a prefix. Missing history is an
            // explicit result, not silence that strands the receiver indefinitely.
            var batch = new List<SimulationCommandMessage>();
            long expected = request.FromSequence;
            if (request.Epoch == _commandEpoch)
                foreach (SimulationCommandMessage command in _commandJournal)
                {
                    if (command.Sequence < expected) continue;
                    if (command.Sequence != expected || command.Epoch != request.Epoch) break;
                    batch.Add(command);
                    if (++expected > request.ToSequence) break;
                }
            bool available = expected > request.ToSequence;
            if (available)
                for (int i = 0; i < batch.Count; i++) SendTo(from, batch[i]);
            SendTo(from, new CommandReplayResultMessage
            {
                Epoch = request.Epoch, FromSequence = request.FromSequence,
                ToSequence = request.ToSequence, Available = available,
            });
        }

        private void HandleCommandReplayResult(CommandReplayResultMessage result)
        {
            if (Role != SessionRole.Client || _worldSyncSuspended || result.Epoch != _commandEpoch ||
                result.FromSequence != _lastReplayRequestFrom || result.ToSequence != _lastReplayRequestTo)
                return;
            if (!result.Available) RequestCommandRecovery("host no longer retains the requested command range");
            // A success response cannot advance the cursor: only receiving the commands
            // themselves does that. A missing suffix still follows the retry deadline.
        }
    }
}
