using CS2MPMod.Core.Diagnostics;
using CS2MPMod.Core.Protocol.Messages;

namespace CS2MPMod.Game.Diagnostics
{
    internal static class OperationTrace
    {
        private static readonly OperationDiagnostics State = new OperationDiagnostics();
        private static long _nextSummaryMs;

        public static void Reset() { State.Reset(); _nextSummaryMs = 0; }
        public static void ClearEpoch() => State.ClearEpoch();
        public static DiagnosticReportMessage CreateReport(System.Guid id, long epoch, long now) =>
            State.CreateReport(id, epoch, now);

        public static void Capture(long operation, int player, string prefab)
        {
            Write(new OperationDiagnosticRecord {
                Session = State.Session, Epoch = Mod.Service?.Session.CommandEpoch,
                Player = player, Operation = operation.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Prefab = prefab, Stage = "captured", CaptureResult = "encoded", ApplyResult = "unknown"
            });
        }

        public static void Observe(SimulationCommandMessage message, string stage,
            string operation = null, string prefab = null, string reason = null)
        {
            if (message == null) return;
            long now = Mod.Service != null ? Mod.Service.NowMs : 0;
            var entry = State.Observe(message.Epoch, message.Sequence, message.OriginPlayerId,
                message.CommandId, now, stage, operation, prefab, reason);
            Write(entry, now);
            foreach (var follower in State.FinishFollowers(entry, now)) Write(follower, now);
        }

        public static void Follow(SimulationCommandMessage child, SimulationCommandMessage parent)
        {
            if (child == null || parent == null) return;
            if (!State.Follow(child.Epoch, child.Sequence, child.OriginPlayerId,
                    parent.Epoch, parent.Sequence, parent.OriginPlayerId))
                Observe(child, "commit-unverified", reason: "coalesced-correlation-unavailable");
        }

        private static void Write(OperationDiagnostics.Entry entry, long now)
        {
            Write(new OperationDiagnosticRecord {
                Session = State.Session, Epoch = entry.Epoch, Sequence = entry.Sequence,
                Player = entry.Player, Command = entry.Command, Operation = entry.Operation,
                Prefab = entry.Prefab, Stage = entry.Stage, CaptureResult = "unknown",
                ApplyResult = entry.Stage == "completed" ? "verified-or-equivalent" : entry.Stage,
                ElapsedLocalMs = System.Math.Max(0, entry.UpdatedMs - entry.ReceivedMs),
                QueueMs = entry.QueueMs, ApplyMs = entry.ApplyMs, RecoveryReason = entry.Reason
            });
        }

        private static void Write(OperationDiagnosticRecord record) =>
            SyncLog.Trace(LogTopic.Pipeline, record.Format());

        public static void Pump(long now)
        {
            if (now < _nextSummaryMs) return;
            _nextSummaryMs = now + 5000;
            foreach (string summary in State.Summary(now))
                SyncLog.Trace(LogTopic.Pipeline, "operation-summary session=" + State.Session + " " + summary);
        }
    }
}
