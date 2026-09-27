using System;
using System.IO;
using CS2MPMod.Core.Protocol;

namespace CS2MPMod.Core.Session
{
    /// <summary>
    /// Accumulates the chunks of one incoming blob until the final chunk arrives, then
    /// yields the complete byte array. Every invariant a hostile sender could violate
    /// is checked here: the announced total must never change between chunks, no chunk
    /// may exceed the wire chunk size, the chunk count must match the announced total,
    /// and the byte count must land exactly on it. Kept tiny and game-free so the
    /// transfer logic is unit-testable.
    /// </summary>
    internal sealed class BlobReassembler
    {
        private MemoryStream _buffer = new MemoryStream();
        private bool _completed;

        public BlobReassembler(int expectedBytes, long nowMs)
        {
            if (expectedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(expectedBytes));
            ExpectedBytes = expectedBytes;
            LastChunkAtMs = nowMs;
        }

        public int ExpectedBytes { get; }
        public int ReceivedBytes { get { return _completed ? ExpectedBytes : (int)_buffer.Length; } }
        public int ChunkCount { get; private set; }

        /// <summary>When most recent chunk arrived - lets owner expire stalled transfers.</summary>
        public long LastChunkAtMs { get; private set; }

        /// <summary>Maximum chunks this blob may consist of, derived from its announced size.</summary>
        public int MaxChunks
        {
            get { return (ExpectedBytes / ProtocolConstants.BlobChunkBytes) + 2; }
        }

        /// <summary>
        /// Add one chunk. Throws <see cref="ProtocolException"/> on any inconsistency;
        /// the caller drops the whole blob (and may disconnect the sender).
        /// </summary>
        public void Append(int announcedTotal, byte[] data, long nowMs)
        {
            if (_completed) throw new ProtocolException("Blob has already been completed.");
            if (announcedTotal != ExpectedBytes)
                throw new ProtocolException("Blob total changed mid-transfer: " +
                                            ExpectedBytes + " -> " + announcedTotal + ".");

            int length = data != null ? data.Length : 0;
            if (length > ProtocolConstants.BlobChunkBytes)
                throw new ProtocolException("Blob chunk of " + length + " bytes exceeds the " +
                                            ProtocolConstants.BlobChunkBytes + "-byte chunk cap.");

            if (ChunkCount >= MaxChunks)
                throw new ProtocolException("Blob exceeded its maximum of " + MaxChunks + " chunks.");

            // Reject before MemoryStream grows or any transfer state is changed.
            if (length > ExpectedBytes - ReceivedBytes)
                throw new ProtocolException("Blob chunk exceeds the remaining announced bytes.");

            if (length > 0)
            {
                int required = ReceivedBytes + length; // Validated against ExpectedBytes above.
                if (required > _buffer.Capacity)
                {
                    // MemoryStream normally doubles without regard to the announced total.
                    // Grow lazily, but never reserve more than this transfer can contain.
                    long capacity = Math.Max(required, (long)_buffer.Capacity * 2);
                    _buffer.Capacity = (int)Math.Min(ExpectedBytes, capacity);
                }
                _buffer.Write(data, 0, length);
            }
            ChunkCount++;
            LastChunkAtMs = nowMs;
        }

        /// <summary>
        /// Finish the transfer: only valid when the byte count matches the announcement
        /// exactly. A short or padded blob is a protocol violation, not a best effort.
        /// </summary>
        public byte[] Complete()
        {
            if (_completed) throw new ProtocolException("Blob has already been completed.");
            if (ReceivedBytes != ExpectedBytes)
                throw new ProtocolException("Blob ended at " + ReceivedBytes + "/" + ExpectedBytes + " bytes.");
            // Capacity is capped at ExpectedBytes during growth, so a complete buffer has
            // exactly the required length. Transfer ownership without a full-size copy.
            byte[] result = _buffer.GetBuffer();
            if (result.Length != ExpectedBytes)
                throw new ProtocolException("Completed blob buffer has an unexpected capacity.");
            _completed = true;
            _buffer.Dispose();
            _buffer = null;
            return result;
        }
    }
}
