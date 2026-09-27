using System;
using System.IO;
using System.Linq;
using CS2MPMod.Core.Diagnostics;
using CS2MPMod.Core.Protocol;
using CS2MPMod.Core.Protocol.Messages;
using CS2MPMod.Core.Session;

internal static class DiagnosticDeliveryTests
{
    private static void Check(bool ok) { if (!ok) throw new Exception("Diagnostic delivery invariant failed"); }
    private static DiagnosticReportMessage Report() => new DiagnosticReportMessage {
        ReportId = Guid.NewGuid(), Epoch = 3,
        Rows = new[] { new DiagnosticReportMessage.Row { Player = 2, Received = 10, Completed = 8, Pending = 1, OldestSequence = 9, OldestAgeMs = 50 } }
    };
    public static void Contract()
    {
        string line = new OperationDiagnosticRecord { Session = "test", Player = 2,
            Prefab = "private\n stage=completed", Stage = "received" }.Format();
        Check(!line.Contains("\n") && line.Contains("sequence=unknown") && line.Contains("queueMs=unknown") &&
            line.Contains("captureResult=unknown") && line.Contains("applyResult=unknown") && line.Contains("recoveryReason=unknown"));
        Check(line.Split(new[] { " stage=" }, StringSplitOptions.None).Length == 2);
    }
    public static void UnicodeContract()
    {
        string emoji = "\uD83C\uDFD9";
        string line = new OperationDiagnosticRecord { Prefab = "Bygg " + emoji }.Format();
        Check(line.Contains("prefab=Bygg%20%F0%9F%8F%99 "));
        line = new OperationDiagnosticRecord { Prefab = new string('a', 199) + emoji }.Format();
        Check(line.Contains("prefab=" + new string('a', 199) + " stage="));
        line = new OperationDiagnosticRecord { Prefab = "\uD800x\uDC00\n", RecoveryReason = "\uD800" }.Format();
        Check(line.Contains("prefab=%EF%BF%BDx%EF%BF%BD%0A "));
        Check(line.Contains("recoveryReason=%EF%BF%BD ") && !line.Contains("\n"));
    }
    public static void ReportEpochIsolation()
    {
        var state = new OperationDiagnostics();
        state.Observe(1, 99, 2, 1, 0, "received");
        state.Observe(2, 1, 2, 1, 100, "completed");
        var summaries = state.Summary(200);
        Check(summaries.Length == 2);
        Check(summaries[0].Contains("epoch=1 player=2 received=99") && summaries[0].Contains("pending=1 "));
        Check(summaries[1].Contains("epoch=2 player=2 received=1") && summaries[1].Contains("pending=0 "));
        var report = state.CreateReport(Guid.NewGuid(), 2, 200);
        Check(report.Rows.Length == 1 && report.Rows[0].Received == 1 && report.Rows[0].Pending == 0);
        Check(state.CreateReport(Guid.NewGuid(), 3, 200).Rows.Length == 0);
        Check(state.CreateReport(Guid.NewGuid(), 1, 200).Rows[0].OldestSequence == 99);
    }
    public static void Codec()
    {
        var codec = MessageCodec.CreateDefault();
        var original = Report();
        var decoded = (DiagnosticReportMessage)codec.Decode(codec.Encode(original));
        Check(decoded.ReportId == original.ReportId && decoded.Rows[0].OldestSequence == 9);
        original.Rows = new DiagnosticReportMessage.Row[33];
        bool rejected = false;
        try { codec.Encode(original); } catch (ProtocolException) { rejected = true; }
        Check(rejected);
    }
    public static void HostAcknowledgesDurableStoreOnly()
    {
        var host = new Program.Fixture(SessionRole.Host);
        int calls = 0;
        bool persisted = false;
        host.Session.StoreDiagnosticReport = (sender, report) => { Check(sender == 2); calls++; return persisted; };
        var packet = Report();
        host.Receive(packet);
        Check(calls == 1 && !host.Transport.Messages<DiagnosticReportMessage>().Any());
        host.Receive(packet);
        Check(calls == 1); // rate-limited even when the disk failed
        persisted = true;
        host.Now += 10000;
        host.Receive(packet);
        Check(calls == 2 && host.Transport.Messages<DiagnosticReportMessage>().Single().Acknowledgement);
    }
    public static void ClientAcknowledgementDirection()
    {
        var client = new Program.Fixture();
        int acks = 0;
        client.Session.DiagnosticReportAcknowledged = id => acks++;
        var packet = Report();
        client.Receive(packet); // Host must not push a report into a client's outbox.
        Check(acks == 0);
        client.Receive(new DiagnosticReportMessage { ReportId = packet.ReportId, Acknowledgement = true });
        Check(acks == 1);
        client.Session.SendDiagnosticReport(packet);
        Check(client.Transport.Messages<DiagnosticReportMessage>().Count() == 1);
    }
    public static void UnapprovedPeerCannotUpload()
    {
        var host = new Program.Fixture(SessionRole.Host);
        var peer = Program.Get<System.Collections.Generic.Dictionary<int, Peer>>(host.Session, "_peers")[2];
        peer.Handshaked = false;
        peer.AwaitingApproval = true;
        int writes = 0;
        host.Session.StoreDiagnosticReport = (sender, report) => { writes++; return true; };
        host.Receive(Report());
        Check(writes == 0 && !host.Transport.Messages<DiagnosticReportMessage>().Any());
    }
    public static void DurableReconnectAndDeduplication()
    {
        string root = Path.Combine(Path.GetTempPath(), "CS2MP-report-test-" + Guid.NewGuid().ToString("N"));
        string target = new string('a', 64), other = new string('b', 64);
        try
        {
            var packet = Report();
            var outbox = new DiagnosticReportStore(Path.Combine(root, "outbox"));
            outbox.Save(target, packet);
            outbox = new DiagnosticReportStore(Path.Combine(root, "outbox")); // process restart
            Check(outbox.Next(other, Guid.Empty) == null);
            Check(outbox.Next(target, packet.ReportId) == null); // do not transmit a live mutable checkpoint
            var recovered = outbox.Next(target, Guid.Empty);
            Check(recovered.ReportId == packet.ReportId);
            var inbox = new DiagnosticReportStore(Path.Combine(root, "inbox"));
            Check(inbox.StoreReceived(recovered) && inbox.StoreReceived(recovered));
            Check(Directory.GetFiles(Path.Combine(root, "inbox"), "*.report").Length == 1);
            Check(outbox.Next(target, Guid.Empty) != null); // storing on host alone cannot erase client copy
            outbox.Acknowledge(target, packet.ReportId);
            Check(outbox.Next(target, Guid.Empty) == null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    public static void ConflictingReportCannotOverwrite()
    {
        string root = Path.Combine(Path.GetTempPath(), "CS2MP-report-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new DiagnosticReportStore(root);
            var packet = Report();
            Check(store.StoreReceived(packet));
            packet.Epoch++;
            Check(!store.StoreReceived(packet));
            bool rejected = false;
            try { store.Save("../escape", packet); } catch (ArgumentException) { rejected = true; }
            Check(rejected);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    public static void LockedReportDoesNotBlock()
    {
        string root = Path.Combine(Path.GetTempPath(), "CS2MP-report-test-" + Guid.NewGuid().ToString("N"));
        string target = new string('a', 64);
        try
        {
            var store = new DiagnosticReportStore(root);
            var first = Report();
            first.ReportId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var second = Report();
            second.ReportId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            store.Save(target, first);
            store.Save(target, second);
            string path = Path.Combine(root, target + "-" + first.ReportId.ToString("N") + ".pending");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                Check(store.Next(target, Guid.Empty).ReportId == second.ReportId);
            Check(store.Next(target, Guid.Empty).ReportId == first.ReportId);
            Check(Directory.GetFiles(root, "*.pending").Length == 2);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    public static void RetryRotation()
    {
        string root = Path.Combine(Path.GetTempPath(), "CS2MP-report-test-" + Guid.NewGuid().ToString("N"));
        string target = new string('a', 64);
        try
        {
            var store = new DiagnosticReportStore(root);
            var first = Report();
            first.ReportId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var second = Report();
            second.ReportId = Guid.Parse("00000000-0000-0000-0000-000000000002");
            var live = Report();
            live.ReportId = Guid.Parse("00000000-0000-0000-0000-000000000003");
            store.Save(target, first);
            store.Save(target, second);
            store.Save(target, live);
            Check(store.Next(target, live.ReportId).ReportId == first.ReportId);
            Check(store.Next(target, live.ReportId, first.ReportId).ReportId == second.ReportId);
            Check(store.Next(target, live.ReportId, second.ReportId).ReportId == first.ReportId);
            Check(Directory.GetFiles(root, "*.pending").Length == 3);
            store.Acknowledge(target, second.ReportId);
            Check(store.Next(target, live.ReportId, second.ReportId).ReportId == first.ReportId);
            Check(store.Next(new string('b', 64), Guid.Empty, first.ReportId) == null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    public static void FullStoresPreserveEvidence()
    {
        string root = Path.Combine(Path.GetTempPath(), "CS2MP-report-test-" + Guid.NewGuid().ToString("N"));
        string target = new string('a', 64);
        try
        {
            var outbox = new DiagnosticReportStore(Path.Combine(root, "outbox"));
            var first = Report();
            outbox.Save(target, first);
            for (int i = 1; i < 128; i++) outbox.Save(target, Report());
            bool full = false;
            try { outbox.Save(target, Report()); } catch (IOException) { full = true; }
            Check(full && Directory.GetFiles(Path.Combine(root, "outbox"), "*.pending").Length == 128);
            first.Epoch++;
            outbox.Save(target, first); // Existing checkpoints can still be refreshed at capacity.
            Check(outbox.Load(target, first.ReportId).Epoch == first.Epoch);
            var inbox = new DiagnosticReportStore(Path.Combine(root, "inbox"));
            Check(inbox.StoreReceived(first));
            for (int i = 1; i < 200; i++) Check(inbox.StoreReceived(Report()));
            Check(!inbox.StoreReceived(Report()));
            Check(inbox.StoreReceived(first)); // Lost acknowledgement can be recovered even at capacity.
            Check(Directory.GetFiles(Path.Combine(root, "inbox"), "*.report").Length == 200);
            Check(outbox.Load(target, first.ReportId) != null);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
