using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using CS2MPMod.Core.Diagnostics;
using CS2MPMod.Core.Networking;
using CS2MPMod.Core.Networking.Tcp;
using CS2MPMod.Core.Protocol;
using CS2MPMod.Core.Protocol.Messages;
using CS2MPMod.Core.Session;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("Ordered commands dispatch once", Ordered),
            ("Deferred verification is bounded, fair and completes once", DeferredVerificationTests.DelayedAndBounded),
            ("Expired or cleared verification never applies a late effect", DeferredVerificationTests.ExpiryAndClear),
            ("Verified commit effect runs once and never before verification", VerifiedCommitEffectTests.VerificationAndDuplicates),
            ("Partially failing commit effect is not implicitly retried", VerifiedCommitEffectTests.PartialFailureNotRetried),
            ("Superseded and post-resume snapshot chunks cannot replace the current world", BlobSessionTests.SupersededSnapshotChunks),
            ("Aborted snapshot is cleared and the next epoch can succeed", BlobSessionTests.AbortedSnapshotChunks),
            ("Rejected blob channels preserve unrelated live progress", BlobSessionTests.RejectionPreservesOtherProgress),
            ("Stalled blob cleanup preserves unrelated live progress", BlobSessionTests.ExpiryPreservesOtherProgress),
            ("Session drops invalid blobs and delivers independent valid retries", BlobSessionTests.InvalidThenValid),
            ("Session rejects unknown, oversize, wrong-epoch and client-origin blobs", BlobSessionTests.RejectUnapprovedTransfers),
            ("Blob completion transfers buffer ownership once without copying", BlobSafetyTests.CompletionTransfersOwnership),
            ("Blob buffer grows lazily without exceeding announced size", BlobSafetyTests.BoundedCapacity),
            ("Network reader rejects invalid and overflowing slices", NetworkReaderSafetyTests.InvalidSlices),
            ("Network reader respects slice boundaries without consuming rejected reads", NetworkReaderSafetyTests.SliceIsolation),
            ("Blob overflow is rejected before buffer mutation", BlobSafetyTests.RejectBeforeGrowth),
            ("Blob wire lengths are strictly bounded", BlobSafetyTests.RejectWireLengths),
            ("Diagnostic contract preserves unknowns and escapes injection", DiagnosticDeliveryTests.Contract),
            ("Diagnostic summaries and reports isolate epochs", DiagnosticDeliveryTests.ReportEpochIsolation),
            ("Diagnostic text handles Unicode boundaries and malformed surrogates", DiagnosticDeliveryTests.UnicodeContract),
            ("Numeric report codec enforces bounded shape", DiagnosticDeliveryTests.Codec),
            ("Unapproved peer cannot upload diagnostic reports", DiagnosticDeliveryTests.UnapprovedPeerCannotUpload),
            ("Host acknowledges only durable report storage", DiagnosticDeliveryTests.HostAcknowledgesDurableStoreOnly),
            ("Report acknowledgements obey client/host direction", DiagnosticDeliveryTests.ClientAcknowledgementDirection),
            ("Report outbox survives restart and same-host reconnect", DiagnosticDeliveryTests.DurableReconnectAndDeduplication),
            ("Conflicting report IDs cannot overwrite host evidence", DiagnosticDeliveryTests.ConflictingReportCannotOverwrite),
            ("Locked reports do not block other pending reports", DiagnosticDeliveryTests.LockedReportDoesNotBlock),
            ("Full report stores preserve evidence and allow retries", DiagnosticDeliveryTests.FullStoresPreserveEvidence),
            ("Report retries rotate without deleting unacknowledged evidence", DiagnosticDeliveryTests.RetryRotation),
            ("Missing first command requests replay", MissingFirst),
            ("Out of order commands wait for the gap", OutOfOrder),
            ("Host returns canonical sequence to the sender", EchoSender),
            ("Sender acknowledgement never reapplies its edit", OwnEcho),
            ("Late join starts after snapshot baseline", LateJoin),
            ("Snapshot replaces an incomplete old command prefix", SnapshotClearsGap),
            ("Abort preserves old stream for replay", AbortPreservesStream),
            ("Completed and stale Begin cannot reopen a barrier", StaleBegin),
            ("Replay advances across multiple ranges without live traffic", MultiRangeReplay),
            ("Expired journal reports failure and requests recovery", ExpiredHistory),
            ("Pending command count is bounded", PendingCountLimit),
            ("Pending command bytes are bounded", PendingByteLimit),
            ("Journal has a byte budget and owns replay bodies", JournalBudget),
            ("Replay retries and escalates on deadline", RetryDeadline),
            ("Successful replay status cannot skip missing commands", ForgedSuccess),
            ("Old epoch commands and replay cannot mutate a new world", OldEpoch),
            ("Three resync requests do not kick a peer", RepeatedResync),
            ("Replay requests are strictly bounded", InvalidReplayRange),
            ("Invalid command lengths are rejected", InvalidCommandLength),
            ("TCP close survives a full data queue exactly once", TcpCloseAtCapacity),
            ("One host and three clients share a continuous command stream", FourPlayers),
            ("A missing predecessor can drain a full reorder buffer", FullBufferCanDrain),
            ("Slow progressing replay is not treated as stalled", SlowReplay),
            ("Recovery request survives a host cooldown", RecoveryRetry),
            ("Closed admission slots do not delay shutdown", ClosedShutdown),
            ("Late building target cancels held recovery", RecoveryTests.LateTarget),
            ("Distinct building commands retain separate recovery evidence", RecoveryTests.DistinctTargets),
            ("Unresolved building target still requires recovery", RecoveryTests.MissingTarget),
            ("Recovery inbox consumes evidence exactly once", RecoveryTests.Inbox),
            ("Recovery cooldown retains the first pending fault", RecoveryTests.Cooldown),
            ("Aborted or ignored recovery is retried after cooldown", RecoveryTests.Aborted),
            ("Completed snapshot supersedes queued recovery", RecoveryTests.Completed),
            ("New session resets recovery timing and evidence", RecoveryTests.NewSession),
            ("Diagnostic receipt is not a native commit", OperationDiagnosticsTests.ReceiptIsNotCommit),
            ("Coalesced operations await and share verified completion", OperationDiagnosticsTests.CoalescedCompletion),
            ("Coalesced rejection is not reported as success", OperationDiagnosticsTests.CoalescedFailure),
            ("Diagnostic links reject cycles and cross-epoch work", OperationDiagnosticsTests.CoalescedBoundaries),
            ("Submission cannot erase an unverified result", OperationDiagnosticsTests.UnverifiedIsNotSubmitted),
            ("Route graph awaits identity before diagnostic completion", OperationDiagnosticsTests.RouteAwaitingIdentity),
            ("Unverified native commit remains diagnostically pending", OperationDiagnosticsTests.UnverifiedCommitStaysPending),
            ("Queue timing remains stable through retries and duplicate commits", OperationDiagnosticsTests.QueueAndApplyTiming),
            ("Unobserved timing is unknown rather than zero", OperationDiagnosticsTests.UnknownTimingIsNotZero),
            ("Operation diagnostic memory is bounded", OperationDiagnosticsTests.Bounded),
            ("Epoch and session reset diagnostic state", OperationDiagnosticsTests.EpochAndSession),
            ("Later completion does not hide an earlier pending operation", OperationDiagnosticsTests.CompletionDoesNotSkipGap),
        };
        int failures = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); }
            catch (Exception ex) { failures++; Console.WriteLine("FAIL " + test.Name + ": " + ex.GetBaseException().Message); }
        }
        Console.WriteLine($"{tests.Length - failures}/{tests.Length} passed");
        return failures == 0 ? 0 : 1;
    }

    private static void Ordered()
    {
        var f = new Fixture();
        f.Receive(Command(1), Command(2), Command(2), Command(3));
        Check(f.Observer.Commands.Select(x => x.Sequence).SequenceEqual(new long[] { 1, 2, 3 }), "duplicates or lost order");
    }

    private static void MissingFirst()
    {
        var f = new Fixture();
        f.Receive(Command(2));
        var request = f.Transport.Messages<CommandReplayRequestMessage>().Single();
        Check(request.FromSequence == 1 && request.ToSequence == 1, "missing initial range");
        f.Receive(Command(1));
        Check(f.Observer.Commands.Count == 2, "gap did not drain");
    }

    private static void OutOfOrder()
    {
        var f = new Fixture();
        f.Receive(Command(1), Command(3));
        Check(f.Observer.Commands.Count == 1, "applied beyond gap");
        f.Receive(Command(2));
        Check(f.Observer.Commands.Select(x => x.Sequence).SequenceEqual(new long[] { 1, 2, 3 }), "incorrect replay order");
    }

    private static void EchoSender()
    {
        var f = new Fixture(SessionRole.Host);
        f.AddPeer(3, 3);
        f.Receive(Command(0, origin: 999));
        var sends = f.Transport.Sent.Where(x => x.Message is SimulationCommandMessage).ToArray();
        Check(sends.Length == 2, "host excluded sender from global sequence");
        Check(sends.All(x => ((SimulationCommandMessage)x.Message).OriginPlayerId == 2), "sender identity not stamped");
    }

    private static void OwnEcho()
    {
        var f = new Fixture();
        f.Receive(Command(1, 2), Command(2));
        Check(f.Observer.Commands.Count == 1 && f.Observer.Commands[0].Sequence == 2, "own edit applied twice or blocked successor");
    }

    private static void Install(Fixture f, long epoch, long baseline)
    {
        f.Receive(new WorldSyncControlMessage(epoch, WorldSyncStage.Begin),
            new WorldSyncControlMessage(epoch, WorldSyncStage.Resume, 1f, baseline));
    }

    private static void LateJoin()
    {
        var f = new Fixture();
        Install(f, 7, 9000);
        f.Receive(Command(9001, epoch: 7));
        Check(f.Observer.Commands.Single().Sequence == 9001, "late join waiting for historical edits");
        Check(!f.Transport.Messages<CommandReplayRequestMessage>().Any(), "replaying snapshot contents");
    }

    private static void SnapshotClearsGap()
    {
        var f = new Fixture();
        f.Receive(Command(2));
        Install(f, 1, 100);
        f.Receive(Command(1), Command(101, epoch: 1));
        Check(f.Observer.Commands.Single().Sequence == 101, "old pending command survived install");
        Check(Get<SortedDictionary<long, SimulationCommandMessage>>(f.Session, "_pendingCommands").Count == 0, "old pending retained");
    }

    private static void AbortPreservesStream()
    {
        var f = new Fixture();
        f.Receive(Command(1), Command(3));
        f.Receive(new WorldSyncControlMessage(1, WorldSyncStage.Begin), new WorldSyncControlMessage(1, WorldSyncStage.Abort));
        f.Receive(Command(2));
        Check(f.Observer.Commands.Select(x => x.Sequence).SequenceEqual(new long[] { 1, 2, 3 }), "abort skipped old stream");
    }

    private static void StaleBegin()
    {
        var f = new Fixture();
        Install(f, 5, 50);
        f.Receive(new WorldSyncControlMessage(4, WorldSyncStage.Begin), new WorldSyncControlMessage(5, WorldSyncStage.Begin));
        Check(!f.Session.WorldSyncSuspended, "stale begin reopened finished epoch");
    }

    private static void MultiRangeReplay()
    {
        var host = new Fixture(SessionRole.Host);
        for (int i = 0; i < 600; i++) host.Session.SendCommand(0, 1, Array.Empty<byte>());
        var client = new Fixture();
        client.Receive(Command(600));
        int handled = 0;
        for (int round = 0; round < 5; round++)
        {
            var requests = client.Transport.Messages<CommandReplayRequestMessage>().Skip(handled).ToArray();
            if (requests.Length == 0) break;
            handled += requests.Length;
            host.Transport.Sent.Clear();
            host.Receive(requests);
            client.Receive(host.Transport.Sent.Select(x => x.Message).ToArray());
        }
        Check(client.Observer.Commands.Count == 600 && handled == 3, "large replay did not finish without new live messages");
    }

    private static void ExpiredHistory()
    {
        var host = new Fixture(SessionRole.Host);
        for (int i = 0; i < 5000; i++) host.Session.SendCommand(0, 1, Array.Empty<byte>());
        var client = new Fixture();
        client.Receive(Command(5000));
        host.Transport.Sent.Clear();
        host.Receive(client.Transport.Messages<CommandReplayRequestMessage>().Single());
        Check(!host.Transport.Messages<SimulationCommandMessage>().Any(), "partial replay escaped");
        var result = host.Transport.Messages<CommandReplayResultMessage>().Single();
        Check(!result.Available, "evicted history advertised available");
        client.Receive(result);
        Check(client.Session.CommandRecoveryPending && client.Transport.Messages<ResyncRequestMessage>().Count() == 1, "no snapshot fallback");
        Install(client, 1, 5000);
        client.Receive(Command(5001, epoch: 1));
        Check(!client.Session.CommandRecoveryPending && client.Observer.Commands.Count == 1, "recovery did not unblock commands");
    }

    private static void PendingCountLimit()
    {
        var f = new Fixture();
        f.Receive(Enumerable.Range(2, 5001).Select(i => Command(i)).ToArray());
        Check(f.Session.CommandRecoveryPending, "unbounded reorder queue");
        Check(Get<SortedDictionary<long, SimulationCommandMessage>>(f.Session, "_pendingCommands").Count == 0, "overflow retained incomplete prefix");
        Check(f.Transport.Messages<ResyncRequestMessage>().Count() == 1, "overflow caused recovery storm");
    }

    private static void PendingByteLimit()
    {
        var f = new Fixture();
        for (int i = 2; i < 40; i++)
            f.Receive(new SimulationCommandMessage(1, 0, i, 1, new byte[300 * 1024]));
        Check(f.Session.CommandRecoveryPending, "missing byte budget");
        Check(Get<long>(f.Session, "_pendingCommandBytes") == 0, "pending byte accounting leaked");
    }

    private static void JournalBudget()
    {
        var f = new Fixture(SessionRole.Host);
        var body = new byte[] { 7 };
        f.Session.SendCommand(0, 1, body);
        body[0] = 99;
        f.Transport.Sent.Clear();
        f.Receive(new CommandReplayRequestMessage(1, 1));
        Check(f.Transport.Messages<SimulationCommandMessage>().Single().Body[0] == 7, "caller mutated journal");
        for (int i = 0; i < 70; i++)
        {
            f.Session.SendCommand(0, 1, new byte[300 * 1024]);
            f.Transport.Sent.Clear();
            f.Observer.Commands.Clear();
        }
        Check(Get<long>(f.Session, "_journalBytes") <= 16L * 1024 * 1024, "journal exceeds byte budget");
        f.Receive(new CommandReplayRequestMessage(1, 1));
        Check(!f.Transport.Messages<CommandReplayResultMessage>().Last().Available, "byte-evicted history still advertised");
    }

    private static void RetryDeadline()
    {
        var f = new Fixture();
        f.Receive(Command(2));
        for (int i = 1; i <= 5; i++) f.Session.Update(100 + i * 1000);
        Check(f.Transport.Messages<CommandReplayRequestMessage>().Count() == 5, "retry count not bounded");
        Check(f.Session.CommandRecoveryPending, "stalled replay did not escalate");
    }

    private static void ForgedSuccess()
    {
        var f = new Fixture();
        f.Receive(Command(2), new CommandReplayResultMessage { FromSequence = 1, ToSequence = 1, Available = true });
        Check(f.Observer.Commands.Count == 0, "status skipped actual missing command");
    }

    private static void OldEpoch()
    {
        var f = new Fixture();
        Install(f, 2, 10);
        f.Receive(Command(11, epoch: 1), new CommandReplayResultMessage { Epoch = 1, FromSequence = 11, ToSequence = 11 });
        Check(!f.Session.CommandRecoveryPending && f.Observer.Commands.Count == 0, "stale data affected new epoch");
        f.Receive(Command(11, epoch: 2));
        Check(f.Observer.Commands.Count == 1, "new epoch blocked");
        var host = new Fixture(SessionRole.Host);
        host.Session.SendCommand(0, 1, Array.Empty<byte>());
        Check(host.Session.BeginWorldSync(1, 1f, new[] { new ConnectionId(2) }), "host barrier failed");
        Check(host.Session.ResumeWorldSync(1, 1f, new[] { new ConnectionId(2) }), "host resume failed");
        Check(host.Transport.Messages<WorldSyncControlMessage>().Last().CommandSequence == 1, "host omitted snapshot baseline");
        host.Transport.Sent.Clear();
        host.Receive(new CommandReplayRequestMessage(1, 1, 0));
        Check(!host.Transport.Messages<SimulationCommandMessage>().Any(), "host replayed pre-snapshot history");
    }

    private static void RepeatedResync()
    {
        var f = new Fixture(SessionRole.Host);
        foreach (long time in new long[] { 10000, 16000, 22000 })
        {
            f.Now = time;
            f.Receive(new ResyncRequestMessage(2, "test recovery"));
        }
        f.Receive(new Heartbeat(22001), Command(0, 2));
        Check(f.Transport.Disconnected.Count == 0, "recovery quota kicked legitimate peer");
    }

    private static void InvalidReplayRange()
    {
        var f = new Fixture(SessionRole.Host);
        f.Receive(new CommandReplayRequestMessage(1, 257));
        Check(f.Transport.Disconnected.Count == 1, "oversized replay range accepted");
    }

    private static void InvalidCommandLength()
    {
        var codec = MessageCodec.CreateDefault();
        byte[] payload = codec.Encode(Command(1));
        for (int i = payload.Length - 4; i < payload.Length; i++) payload[i] = 255;
        bool rejected = false;
        try { codec.Decode(payload); } catch (ProtocolException) { rejected = true; }
        Check(rejected, "negative body length accepted");
    }

    private static void TcpCloseAtCapacity()
    {
        var transport = new TcpServerTransport(NullModLogger.Instance);
        var connections = Get<ConcurrentDictionary<int, FramedConnection>>(transport, "_connections");
        // No listener or connection is opened. This socket only backs an unstarted endpoint.
        using (var socket = new TcpClient())
        {
            connections[2] = new FramedConnection(new ConnectionId(2), socket);
            Set(transport, "_active", true);
            var queue = Get<ConcurrentQueue<TransportEvent>>(transport, "_events");
            for (int i = 0; i < TcpServerTransport.MaxQueuedEvents; i++)
                queue.Enqueue(TransportEvent.Data(new ConnectionId(2), Array.Empty<byte>()));
            Set(transport, "_queuedEvents", TcpServerTransport.MaxQueuedEvents);
            var closed = transport.GetType().GetMethod("HandleClosed", BindingFlags.Instance | BindingFlags.NonPublic);
            closed.Invoke(transport, new object[] { new ConnectionId(2), "test close" });
            closed.Invoke(transport, new object[] { new ConnectionId(2), "duplicate close" });
            Check(connections.Count == 1, "admission slot released before close notification");
            var sink = new List<TransportEvent>();
            transport.Poll(sink);
            Check(sink.Count(x => x.Type == TransportEventType.Disconnected) == 1, "close dropped or duplicated");
            Check(connections.Count == 0, "closed peer retained after notification");
            sink.Clear();
            transport.Poll(sink);
            Check(sink.Count == 0, "close delivered twice");
            Set(transport, "_active", false);
        }
    }

    private static void FourPlayers()
    {
        var host = new Fixture(SessionRole.Host);
        host.AddPeer(3, 3); host.AddPeer(4, 4);
        var clients = new[] { new Fixture(), new Fixture(), new Fixture() };
        for (int i = 0; i < clients.Length; i++) Set(clients[i].Session, "<LocalPlayerId>k__BackingField", i + 2);
        host.Session.SendCommand(0, 1, Array.Empty<byte>());
        for (int i = 2; i <= 4; i++)
        {
            host.Transport.Incoming.Enqueue(TransportEvent.Data(new ConnectionId(i),
                host.Transport.Codec.Encode(Command(0, origin: i))));
        }
        host.Session.Update(100);
        for (int i = 0; i < clients.Length; i++)
        {
            clients[i].Receive(host.Transport.Sent.Where(x => x.Target.Value == i + 2).Select(x => x.Message).ToArray());
            Check(Get<long>(clients[i].Session, "_lastReceivedCommandSequence") == 4, "client lacks canonical prefix");
            Check(clients[i].Observer.Commands.Count == 3, "own edit reapplied or another player's edit missing");
            Check(!clients[i].Transport.Messages<CommandReplayRequestMessage>().Any(), "normal edits created artificial gaps");
        }
    }

    private static void FullBufferCanDrain()
    {
        var f = new Fixture();
        f.Receive(Enumerable.Range(2, 2048).Select(i => Command(i)).ToArray());
        Check(!f.Session.CommandRecoveryPending, "buffer filled too early");
        f.Receive(Command(1));
        Check(!f.Session.CommandRecoveryPending && f.Observer.Commands.Count == 2049, "predecessor at capacity triggered unnecessary resync");
    }

    private static void SlowReplay()
    {
        var f = new Fixture();
        f.Receive(Command(15));
        for (int i = 1; i < 15; i++) { f.Now += 900; f.Receive(Command(i)); }
        Check(!f.Session.CommandRecoveryPending && f.Observer.Commands.Count == 15, "progressing stream was abandoned");
        Check(f.Transport.Messages<CommandReplayRequestMessage>().Count() == 1, "progressing batch redundantly resent");
    }

    private static void RecoveryRetry()
    {
        var f = new Fixture();
        f.Receive(Command(2), new CommandReplayResultMessage { FromSequence = 1, ToSequence = 1, Available = false });
        f.Now = 30200;
        f.Receive(new Heartbeat(30200));
        Check(f.Transport.Messages<ResyncRequestMessage>().Count() == 2, "cooldown refusal stranded recovery");
        f.Now = 121000;
        f.Receive(new Heartbeat(121000));
        Check(f.Session.Status == SessionStatus.Offline, "recovery waited without an upper bound");
    }

    private static void ClosedShutdown()
    {
        var transport = new TcpServerTransport(NullModLogger.Instance);
        using (var socket = new TcpClient())
        {
            Get<ConcurrentDictionary<int, FramedConnection>>(transport, "_connections")[2] =
                new FramedConnection(new ConnectionId(2), socket);
            Set(transport, "_active", true);
            transport.GetType().GetMethod("HandleClosed", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(transport, new object[] { new ConnectionId(2), "closed before shutdown" });
            transport.ShutdownAfterFlush(0);
            Check(Get<ConcurrentDictionary<int, FramedConnection>>(transport, "_connections").Count == 0, "closed endpoint retained");
            Check(Get<ConcurrentDictionary<int, string>>(transport, "_closedConnections").Count == 0, "closed notification retained after shutdown");
        }
    }

    internal static SimulationCommandMessage Command(long sequence, int origin = 1, long epoch = 0) =>
        new SimulationCommandMessage(origin, 0, sequence, 1, Array.Empty<byte>(), epoch);

    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Set(object instance, string field, object value) =>
        instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);

    internal static T Get<T>(object instance, string field) =>
        (T)instance.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

    internal sealed class Fixture
    {
        public readonly MultiplayerSession Session = new MultiplayerSession(NullModLogger.Instance);
        public readonly FakeTransport Transport = new FakeTransport();
        public readonly Recorder Observer = new Recorder();
        private readonly ConnectionId _remote;
        public long Now = 100;

        public Fixture(SessionRole role = SessionRole.Client)
        {
            Set(Session, "<Role>k__BackingField", role);
            Set(Session, "<Status>k__BackingField", SessionStatus.Connected);
            Set(Session, "<LocalPlayerId>k__BackingField", role == SessionRole.Host ? 1 : 2);
            Set(Session, "_transport", Transport);
            Set(Session, "_config", new MultiplayerConfig("Test", "localhost", 0));
            _remote = new ConnectionId(role == SessionRole.Host ? 2 : 1);
            AddPeer(_remote.Value, role == SessionRole.Host ? 2 : 1);
            Session.AddObserver(Observer);
        }

        public void AddPeer(int connection, int player)
        {
            Get<Dictionary<int, Peer>>(Session, "_peers")[connection] =
                new Peer(new ConnectionId(connection)) { PlayerId = player, Handshaked = true, Name = "Test" + player };
        }

        public void Receive(params INetMessage[] messages)
        {
            foreach (var message in messages) Transport.Incoming.Enqueue(TransportEvent.Data(_remote, Transport.Codec.Encode(message)));
            Session.Update(Now++);
        }
    }

    internal sealed class Recorder : SessionObserver
    {
        public readonly List<SimulationCommandMessage> Commands = new List<SimulationCommandMessage>();
        public override void OnCommandReceived(SimulationCommandMessage command) => Commands.Add(command);
    }

    internal sealed class FakeTransport : ITransport
    {
        public readonly MessageCodec Codec = MessageCodec.CreateDefault();
        public readonly Queue<TransportEvent> Incoming = new Queue<TransportEvent>();
        public readonly List<(ConnectionId Target, INetMessage Message)> Sent = new List<(ConnectionId, INetMessage)>();
        public readonly List<ConnectionId> Disconnected = new List<ConnectionId>();
        public bool IsActive { get; private set; } = true;
        public long PendingSendBytes => 0;
        public IEnumerable<T> Messages<T>() => Sent.Select(x => x.Message).OfType<T>();
        public void Send(ConnectionId target, byte[] payload) => Sent.Add((target, Codec.Decode(payload)));
        public int Poll(IList<TransportEvent> sink)
        {
            int count = 0;
            while (Incoming.Count > 0) { sink.Add(Incoming.Dequeue()); count++; }
            return count;
        }
        public void Disconnect(ConnectionId connection) => Disconnected.Add(connection);
        public void DisconnectAfterFlush(ConnectionId connection) => Disconnect(connection);
        public string GetRemoteAddress(ConnectionId connection) => "127.0.0.1";
        public byte[] GetChannelBinding(ConnectionId connection) => Array.Empty<byte>();
        public void Shutdown() => IsActive = false;
        public void ShutdownAfterFlush(int timeoutMs) => Shutdown();
        public void Dispose() => Shutdown();
    }
}
