using System;
using System.Collections.Generic;
using System.Linq;

namespace CS2MPMod.Core.Diagnostics
{
    /// <summary>Bounded local observations, never a network acknowledgement or AppliedThrough.</summary>
    public sealed class OperationDiagnostics
    {
        public sealed class Entry
        {
            public long Epoch, Sequence, ReceivedMs, UpdatedMs;
            public long? FirstApplyMs, CompletedMs;
            public long? QueueMs => FirstApplyMs.HasValue ? Math.Max(0, FirstApplyMs.Value - ReceivedMs) : (long?)null;
            public long? ApplyMs => CompletedMs.HasValue && FirstApplyMs.HasValue
                ? Math.Max(0, CompletedMs.Value - FirstApplyMs.Value) : (long?)null;
            public int Player;
            public ushort Command;
            public string Stage, Operation, Prefab, Reason;
            internal string ParentKey;
        }

        private readonly int _capacity;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly LinkedList<string> _order = new LinkedList<string>();
        public string Session { get; private set; } = Guid.NewGuid().ToString("N");
        public long Evicted { get; private set; }

        public OperationDiagnostics(int capacity = 2048)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public Entry Observe(long epoch, long sequence, int player, ushort command, long nowMs,
            string stage, string operation = null, string prefab = null, string reason = null)
        {
            string key = epoch + ":" + sequence + ":" + player;
            Entry entry;
            if (!_entries.TryGetValue(key, out entry))
            {
                if (_entries.Count == _capacity)
                {
                    _entries.Remove(_order.First.Value); _order.RemoveFirst(); Evicted++;
                }
                entry = new Entry { Epoch = epoch, Sequence = sequence, Player = player,
                    Command = command, ReceivedMs = nowMs };
                _entries.Add(key, entry);
                _order.AddLast(key);
            }
            // A duplicate receipt is not permission to undo a confirmed native commit.
            if (entry.Stage == "completed") return entry;
            if (entry.Stage == "commit-unverified" && stage == "submitted") return entry;
            if (!entry.FirstApplyMs.HasValue && (stage == "decoded" || stage == "applying"))
                entry.FirstApplyMs = nowMs;
            if (stage == "completed") entry.CompletedMs = nowMs;
            entry.UpdatedMs = nowMs;
            entry.Stage = stage;
            if (operation != null) entry.Operation = Limit(operation);
            if (prefab != null) entry.Prefab = Limit(prefab);
            if (reason != null) entry.Reason = Limit(reason);
            return entry;
        }

        public string[] Summary(long nowMs)
        {
            return _entries.Values.GroupBy(x => new { x.Epoch, x.Player })
                .OrderBy(x => x.Key.Epoch).ThenBy(x => x.Key.Player).Select(peer =>
            {
                var pending = peer.Where(x => x.Stage != "completed" && x.Stage != "rejected").ToArray();
                long lastCompleted = peer.Where(x => x.Stage == "completed").Select(x => x.Sequence).DefaultIfEmpty(0).Max();
                return "epoch=" + peer.Key.Epoch + " player=" + peer.Key.Player + " received=" + peer.Max(x => x.Sequence) +
                    " completedObserved=" + lastCompleted + " pending=" + pending.Length +
                    " oldestPendingMs=" + (pending.Length == 0 ? 0 : Math.Max(0, nowMs - pending.Min(x => x.ReceivedMs))) +
                    " oldestPendingSequence=" + (pending.Length == 0 ? 0 : pending.OrderBy(x => x.ReceivedMs).First().Sequence) +
                    " evicted=" + Evicted;
            }).ToArray();
        }

        public Protocol.Messages.DiagnosticReportMessage CreateReport(Guid id, long epoch, long nowMs)
        {
            return new Protocol.Messages.DiagnosticReportMessage {
                ReportId = id, Epoch = epoch, Evicted = Evicted,
                Rows = _entries.Values.Where(x => x.Epoch == epoch).GroupBy(x => x.Player).OrderBy(x => x.Key).Take(32).Select(peer => {
                    var pending = peer.Where(x => x.Stage != "completed" && x.Stage != "rejected")
                        .OrderBy(x => x.ReceivedMs).ToArray();
                    return new Protocol.Messages.DiagnosticReportMessage.Row {
                        Player = peer.Key, Pending = pending.Length, Received = peer.Max(x => x.Sequence),
                        Completed = peer.Where(x => x.Stage == "completed").Select(x => x.Sequence).DefaultIfEmpty(0).Max(),
                        OldestSequence = pending.Length == 0 ? 0 : pending[0].Sequence,
                        OldestAgeMs = pending.Length == 0 ? 0 : Math.Max(0, nowMs - pending[0].ReceivedMs)
                    };
                }).ToArray()
            };
        }

        /// <summary>Link equivalent coalesced work, without retaining message bodies or entries beyond the window.</summary>
        public bool Follow(long epoch, long sequence, int player, long parentEpoch, long parentSequence, int parentPlayer)
        {
            string childKey = epoch + ":" + sequence + ":" + player;
            string parentKey = parentEpoch + ":" + parentSequence + ":" + parentPlayer;
            Entry child, parent;
            if (epoch != parentEpoch || childKey == parentKey ||
                !_entries.TryGetValue(childKey, out child) || !_entries.TryGetValue(parentKey, out parent) ||
                parent.ParentKey != null || parent.Stage == "completed" || parent.Stage == "rejected" || child.Stage == "completed" ||
                _entries.Values.Any(x => x.ParentKey == childKey)) return false;
            child.ParentKey = parentKey;
            return true;
        }

        public Entry[] FinishFollowers(Entry parent, long nowMs)
        {
            if (parent.Stage != "completed" && parent.Stage != "rejected") return Array.Empty<Entry>();
            string parentKey = parent.Epoch + ":" + parent.Sequence + ":" + parent.Player;
            var followers = _entries.Values.Where(x => x.ParentKey == parentKey && x.Stage != "completed").ToArray();
            foreach (Entry follower in followers)
            {
                follower.ParentKey = null;
                Observe(follower.Epoch, follower.Sequence, follower.Player, follower.Command,
                    nowMs, parent.Stage, reason: "coalesced-operation-" + parent.Stage);
            }
            return followers;
        }

        public void Reset()
        {
            ClearEpoch();
            Session = Guid.NewGuid().ToString("N");
        }

        public void ClearEpoch() { _entries.Clear(); _order.Clear(); Evicted = 0; }

        private static string Limit(string value) => value.Length <= 200 ? value : value.Substring(0, 200);
    }
}
