using System;
using CS2MPMod.Core.Sync;

internal static class DeferredVerificationTests
{
    public static void DelayedAndBounded()
    {
        var queue = new DeferredVerificationQueue(2);
        int checks = 0, effects = 0;
        bool ready = false;
        queue.Add(0, () => { checks++; return ready; }, () => effects++);
        queue.Add(0, () => true, () => effects++);
        if (queue.Add(0, () => true, () => effects++)) throw new Exception("Capacity exceeded.");
        queue.Pump(249, 4);
        if (checks != 0 || effects != 0) throw new Exception("Early polling.");
        queue.Pump(250, 1);
        if (checks != 1 || effects != 0) throw new Exception("Budget ignored.");
        queue.Pump(250, 1);
        if (effects != 1) throw new Exception("Unready first item starved second item.");
        ready = true;
        queue.Pump(500, 4);
        queue.Pump(750, 4);
        if (effects != 2 || queue.Count != 0) throw new Exception("Completion duplicated or lost.");
    }
    public static void ExpiryAndClear()
    {
        var queue = new DeferredVerificationQueue(2);
        int effects = 0;
        queue.Add(0, () => true, () => effects++);
        queue.Pump(10000, 4);
        queue.Add(10000, () => true, () => effects++);
        queue.Clear();
        queue.Pump(10250, 4);
        if (effects != 0 || queue.Count != 0) throw new Exception("Expired/cancelled effect ran.");
    }
}
