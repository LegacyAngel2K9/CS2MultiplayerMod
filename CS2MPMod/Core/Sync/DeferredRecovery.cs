using System;

namespace CS2MPMod.Core.Sync
{
    /// <summary>Main-thread, single-slot recovery queue. An attempt is not a completed repair.</summary>
    public sealed class DeferredRecovery<T> where T : class
    {
        private readonly long _cooldownMs;
        private long? _lastAttemptMs;
        private T _pending;

        public DeferredRecovery(long cooldownMs)
        {
            if (cooldownMs <= 0) throw new ArgumentOutOfRangeException(nameof(cooldownMs));
            _cooldownMs = cooldownMs;
        }

        // First evidence explains subsequent symptoms; memory use stays constant.
        public bool Enqueue(T report)
        {
            if (report == null || _pending != null) return false;
            _pending = report;
            return true;
        }

        public bool TryAttempt(long nowMs, bool inFlight, out T report)
        {
            report = null;
            if (_pending == null || inFlight) return false;
            if (_lastAttemptMs.HasValue &&
                (nowMs < _lastAttemptMs.Value || nowMs - _lastAttemptMs.Value < _cooldownMs))
                return false;
            _lastAttemptMs = nowMs;
            report = _pending;
            return true;
        }

        /// <summary>Only a successfully completed snapshot supersedes pending evidence.</summary>
        public void Complete() => _pending = null;

        public void Reset()
        {
            _pending = null;
            _lastAttemptMs = null;
        }
    }
}
