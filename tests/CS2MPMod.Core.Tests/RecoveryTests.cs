using System;
using System.Collections.Generic;
using CS2MPMod.Core.Diagnostics;
using CS2MPMod.Game.Diagnostics;
using CS2MPMod.Game.Sync.Infrastructure;

// Only the game logging sink is replaced. Tests compile the real arbiter,
// report and inbox; they do not simulate Unity's native placement pipeline.
namespace CS2MPMod.Game.Diagnostics
{
    internal static class SyncLog
    {
        public static void Event(LogTopic topic, string text, List<string> lines = null) { }
    }
}

internal static class RecoveryTests
{
    public static void Cooldown()
    {
        var queue = new CS2MPMod.Core.Sync.DeferredRecovery<string>(90000);
        string report;
        queue.Enqueue("first");
        Check(queue.TryAttempt(0, false, out report) && report == "first", "initial attempt blocked");
        queue.Complete();
        Check(queue.Enqueue("second"), "new fault lost");
        Check(!queue.Enqueue("consequence"), "first evidence overwritten");
        Check(!queue.TryAttempt(89999, false, out report), "cooldown bypassed");
        Check(queue.TryAttempt(90000, false, out report) && report == "second", "deferred fault lost");
        Check(!queue.TryAttempt(90000, false, out report), "duplicate request in same tick");
    }

    public static void Aborted()
    {
        var queue = new CS2MPMod.Core.Sync.DeferredRecovery<string>(90000);
        string report;
        queue.Enqueue("missing building");
        queue.TryAttempt(100, false, out report);
        Check(!queue.TryAttempt(100000, true, out report), "request during snapshot");
        // No Complete callback on abort: same evidence must still be available.
        Check(queue.TryAttempt(100001, false, out report) && report == "missing building", "abort lost repair");
    }

    public static void Completed()
    {
        var queue = new CS2MPMod.Core.Sync.DeferredRecovery<string>(90000);
        string report;
        queue.Enqueue("fault");
        queue.Complete(); // A manual/periodic snapshot may repair it before its first attempt.
        Check(!queue.TryAttempt(1000000, false, out report), "obsolete recovery survived snapshot");
    }

    public static void NewSession()
    {
        var queue = new CS2MPMod.Core.Sync.DeferredRecovery<string>(90000);
        string report;
        queue.Enqueue("old city");
        queue.TryAttempt(1000000, false, out report);
        queue.Reset();
        Check(!queue.TryAttempt(0, false, out report), "old city evidence leaked");
        queue.Enqueue("new city");
        Check(queue.TryAttempt(0, false, out report) && report == "new city", "old cooldown leaked");
    }

    private static ResyncReport Target(string subject) => ResyncReport
        .Create("building placement target did not resolve", "object", ResyncEvidence.MissingTarget)
        .About(subject);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void LateTarget()
    {
        ResyncArbiter.Reset();
        var report = Target("epoch=1 sequence=1 origin=2");
        Check(ResyncArbiter.Submit(report, 10000, false) == ResyncVerdict.Held, "not held");
        for (long now = 10200; now < 18000; now += 200)
            Check(ResyncArbiter.Submit(report, now, false) == ResyncVerdict.Held, "retry prematurely settled");
        ResyncArbiter.Withdraw(report.Subsystem, report.Reason, report.Subject, 18000, "target arrived");
        Check(ResyncArbiter.TakeMatured(30000) == null, "repaired target still triggers resync");
    }

    public static void DistinctTargets()
    {
        ResyncArbiter.Reset();
        var a = Target("epoch=1 sequence=1 origin=2");
        var b = Target("epoch=1 sequence=2 origin=2");
        ResyncArbiter.Submit(a, 10000, false);
        ResyncArbiter.Submit(b, 10000, false);
        ResyncArbiter.Withdraw(a.Subsystem, a.Reason, a.Subject, 11000, "target arrived");
        var matured = ResyncArbiter.TakeMatured(22000);
        Check(matured != null && matured.Count == 1 && ReferenceEquals(matured[0], b),
            "withdrawing one building cancelled another's fault");
    }

    public static void MissingTarget()
    {
        ResyncArbiter.Reset();
        var report = Target("epoch=1 sequence=3 origin=2");
        ResyncArbiter.Submit(report, 10000, false);
        Check(ResyncArbiter.TakeMatured(21999) == null, "hold too short");
        var matured = ResyncArbiter.TakeMatured(22000);
        Check(matured != null && matured.Count == 1, "permanent divergence ignored");
        Check(ResyncArbiter.TakeMatured(23000) == null, "recovery emitted twice");
    }

    public static void Inbox()
    {
        SyncInbox.LogWarn = _ => { };
        SyncInbox.Arbitrate = null;
        ResyncReport taken;
        while (SyncInbox.TryTakeResyncRequest(out taken)) { }
        var first = Target("first");
        var second = Target("second");
        SyncInbox.RequestResync(first);
        SyncInbox.RequestResync(second);
        Check(SyncInbox.TryTakeResyncRequest(out taken) && ReferenceEquals(taken, first), "earliest evidence lost");
        Check(!SyncInbox.TryTakeResyncRequest(out taken), "phantom request");
        SyncInbox.RequestResync(second);
        Check(SyncInbox.TryTakeResyncRequest(out taken) && ReferenceEquals(taken, second), "next evidence lost");
        Check(!SyncInbox.TryTakeResyncRequest(out taken), "request consumed twice");
    }
}
