using System;
using CS2MPMod.Core.Sync;

internal static class VerifiedCommitEffectTests
{
    public static void VerificationAndDuplicates()
    {
        var guard = new VerifiedCommitEffect();
        int calls = 0;
        if (guard.TryApply(false, () => calls++) || calls != 0)
            throw new Exception("Unverified commit ran effect.");
        if (!guard.TryApply(true, () => calls++) || calls != 1 ||
            guard.TryApply(true, () => calls++) || calls != 1)
            throw new Exception("Verified effect did not run exactly once.");
    }

    public static void PartialFailureNotRetried()
    {
        var guard = new VerifiedCommitEffect();
        int mutations = 0;
        bool failed = false;
        try { guard.TryApply(true, () => { mutations++; throw new InvalidOperationException(); }); }
        catch (InvalidOperationException) { failed = true; }
        if (!failed || guard.TryApply(true, () => mutations++) || mutations != 1)
            throw new Exception("Effect may have mutated before failure and was retried.");
    }
}
