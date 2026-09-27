using System;
using CS2MPMod.Core.Diagnostics;

internal static class OperationDiagnosticsTests
{
    private static void Check(bool value) { if (!value) throw new Exception("diagnostic invariant failed"); }
    public static void CoalescedCompletion()
    {
        var state = new OperationDiagnostics();
        state.Observe(1, 1, 2, 1, 100, "applying");
        var child = state.Observe(1, 2, 3, 1, 200, "applying");
        Check(state.Follow(1, 2, 3, 1, 1, 2));
        var parent = state.Observe(1, 1, 2, 1, 400, "waiting-identity");
        Check(state.FinishFollowers(parent, 400).Length == 0 && !child.CompletedMs.HasValue);
        state.Observe(1, 1, 2, 1, 500, "completed");
        Check(state.FinishFollowers(parent, 500).Length == 1 && child.ApplyMs == 300);
        Check(state.FinishFollowers(parent, 600).Length == 0);
    }
    public static void CoalescedFailure()
    {
        var state = new OperationDiagnostics();
        state.Observe(1, 1, 2, 1, 100, "applying");
        var child = state.Observe(1, 2, 3, 1, 200, "applying");
        Check(state.Follow(1, 2, 3, 1, 1, 2));
        var parent = state.Observe(1, 1, 2, 1, 400, "rejected");
        Check(state.FinishFollowers(parent, 400).Length == 1);
        Check(child.Stage == "rejected" && !child.CompletedMs.HasValue);
    }
    public static void CoalescedBoundaries()
    {
        var state = new OperationDiagnostics(2);
        state.Observe(1, 1, 2, 1, 100, "applying");
        state.Observe(1, 2, 3, 1, 100, "applying");
        Check(!state.Follow(1, 1, 2, 1, 1, 2));
        Check(!state.Follow(1, 2, 3, 2, 1, 2));
        Check(state.Follow(1, 2, 3, 1, 1, 2));
        Check(!state.Follow(1, 1, 2, 1, 2, 3));
        state.ClearEpoch();
        var parent = state.Observe(2, 1, 2, 1, 200, "completed");
        Check(state.FinishFollowers(parent, 200).Length == 0);
    }
    public static void UnverifiedIsNotSubmitted()
    {
        var state = new OperationDiagnostics();
        var entry = state.Observe(1, 1, 2, 1, 100, "commit-unverified", reason: "mismatch");
        state.Observe(1, 1, 2, 1, 200, "submitted");
        Check(entry.Stage == "commit-unverified" && entry.Reason == "mismatch");
    }
    public static void RouteAwaitingIdentity()
    {
        var state = new OperationDiagnostics();
        state.Observe(1, 5, 2, 1, 100, "received");
        state.Observe(1, 5, 2, 1, 150, "applying");
        state.Observe(1, 5, 2, 1, 200, "submitted");
        var entry = state.Observe(1, 5, 2, 1, 300, "waiting-identity");
        Check(!entry.CompletedMs.HasValue && state.Summary(400)[0].Contains("completedObserved=0 pending=1"));
        state.Observe(1, 5, 2, 1, 500, "completed");
        Check(entry.QueueMs == 50 && entry.ApplyMs == 350);
    }
    public static void UnverifiedCommitStaysPending()
    {
        var state = new OperationDiagnostics();
        var entry = state.Observe(1, 5, 2, 1, 100, "applying");
        state.Observe(1, 5, 2, 1, 200, "commit-unverified");
        Check(!entry.CompletedMs.HasValue && state.Summary(300)[0].Contains("pending=1"));
        state.Observe(1, 5, 2, 1, 400, "retry");
        state.Observe(1, 5, 2, 1, 500, "applying");
        state.Observe(1, 5, 2, 1, 600, "completed");
        Check(entry.FirstApplyMs == 100 && entry.ApplyMs == 500);
    }
    public static void QueueAndApplyTiming()
    {
        var state = new OperationDiagnostics();
        var entry = state.Observe(1, 1, 2, 1, 100, "received");
        Check(!entry.QueueMs.HasValue && !entry.ApplyMs.HasValue);
        state.Observe(1, 1, 2, 1, 150, "decoded");
        state.Observe(1, 1, 2, 1, 200, "retry");
        state.Observe(1, 1, 2, 1, 300, "decoded");
        state.Observe(1, 1, 2, 1, 400, "completed");
        Check(entry.QueueMs == 50 && entry.ApplyMs == 250);
        state.Observe(1, 1, 2, 1, 900, "completed");
        Check(entry.CompletedMs == 400 && entry.ApplyMs == 250);
    }
    public static void UnknownTimingIsNotZero()
    {
        var state = new OperationDiagnostics();
        var entry = state.Observe(1, 1, 2, 1, 100, "completed");
        Check(!entry.QueueMs.HasValue && !entry.ApplyMs.HasValue);
    }
    public static void ReceiptIsNotCommit()
    {
        var state = new OperationDiagnostics();
        state.Observe(1, 10, 2, 1, 100, "received");
        Check(state.Summary(200)[0].Contains("completedObserved=0 pending=1 oldestPendingMs=100"));
        state.Observe(1, 10, 2, 1, 300, "completed");
        state.Observe(1, 10, 2, 1, 400, "received");
        Check(state.Summary(500)[0].Contains("completedObserved=10 pending=0"));
    }
    public static void Bounded()
    {
        var state = new OperationDiagnostics(2);
        for (int i = 1; i <= 100; i++) state.Observe(1, i, 2, 1, i, "received");
        Check(state.Evicted == 98 && state.Summary(100)[0].Contains("pending=2"));
    }
    public static void EpochAndSession()
    {
        var state = new OperationDiagnostics();
        string session = state.Session;
        state.Observe(1, 1, 2, 1, 0, "received");
        state.ClearEpoch();
        Check(state.Summary(100).Length == 0 && session == state.Session);
        state.Reset();
        Check(state.Session != session && state.Evicted == 0);
    }
    public static void CompletionDoesNotSkipGap()
    {
        var state = new OperationDiagnostics();
        state.Observe(1, 1, 2, 1, 0, "received");
        state.Observe(1, 2, 2, 1, 10, "completed");
        Check(state.Summary(100)[0].Contains("completedObserved=2 pending=1 oldestPendingMs=100"));
    }
}
