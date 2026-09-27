using System;

namespace CS2MPMod.Core.Sync
{
    /// <summary>One effect attempt per native transaction, only after verification.
    /// Not a durable operation ledger. An exception may follow mutation, so never retry implicitly.</summary>
    public sealed class VerifiedCommitEffect
    {
        private bool _attempted;

        public bool TryApply(bool verified, Action effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            if (!verified || _attempted) return false;
            _attempted = true;
            effect();
            return true;
        }
    }
}
