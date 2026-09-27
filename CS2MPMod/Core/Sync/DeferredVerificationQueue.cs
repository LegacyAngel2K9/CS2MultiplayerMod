using System;
using System.Collections.Generic;

namespace CS2MPMod.Core.Sync
{
    /// <summary>Bounded, round-robin observation retries. Never replays the construction itself.</summary>
    public sealed class DeferredVerificationQueue
    {
        private sealed class Check
        {
            public long Deadline, Next;
            public Func<bool> Verify;
            public Action Complete;
        }
        private readonly LinkedList<Check> _checks = new LinkedList<Check>();
        private readonly int _capacity;
        public int Count => _checks.Count;
        public DeferredVerificationQueue(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }
        public bool Add(long now, Func<bool> verify, Action complete)
        {
            if (verify == null || complete == null) throw new ArgumentNullException();
            if (_checks.Count >= _capacity) return false;
            _checks.AddLast(new Check { Deadline = now + 10000, Next = now + 250,
                Verify = verify, Complete = complete });
            return true;
        }
        public void Pump(long now, int budget)
        {
            int scan = _checks.Count;
            while (scan-- > 0 && budget > 0 && _checks.First != null)
            {
                var node = _checks.First;
                _checks.RemoveFirst();
                var check = node.Value;
                if (now >= check.Deadline) continue; // Remains unverified; never apply a late side effect.
                if (now < check.Next) { _checks.AddLast(node); continue; }
                budget--;
                if (check.Verify()) check.Complete(); // Already removed, even if the effect throws.
                else { check.Next = now + 250; _checks.AddLast(node); }
            }
        }
        public void Clear() => _checks.Clear();
    }
}
