using System;
using CS2MPMod.Core.Protocol;
using CS2MPMod.Core.Protocol.Messages;
using CS2MPMod.Core.Session;

internal static class BlobSafetyTests
{
    public static void CompletionTransfersOwnership()
    {
        var blob = new BlobReassembler(2, 0);
        blob.Append(2, new byte[] { 3 }, 1);
        bool incompleteRejected = false;
        try { blob.Complete(); } catch (ProtocolException) { incompleteRejected = true; }
        if (!incompleteRejected) throw new Exception("Incomplete transfer completed.");
        blob.Append(2, new byte[] { 4 }, 2);
        var backing = Program.Get<System.IO.MemoryStream>(blob, "_buffer").GetBuffer();
        var result = blob.Complete();
        if (!ReferenceEquals(backing, result) || result.Length != 2 || result[1] != 4 || blob.ReceivedBytes != 2 ||
            Program.Get<System.IO.MemoryStream>(blob, "_buffer") != null)
            throw new Exception("Completion copied or retained the buffer.");
        bool appendRejected = false, duplicateRejected = false;
        try { blob.Append(2, Array.Empty<byte>(), 3); } catch (ProtocolException) { appendRejected = true; }
        try { blob.Complete(); } catch (ProtocolException) { duplicateRejected = true; }
        if (!appendRejected || !duplicateRejected || blob.LastChunkAtMs != 2 || blob.ChunkCount != 2 || result[0] != 3)
            throw new Exception("Completed transfer was reused or mutated.");
    }

    public static void BoundedCapacity()
    {
        int chunkSize = ProtocolConstants.BlobChunkBytes;
        int total = chunkSize * 2 + 1;
        var blob = new BlobReassembler(total, 0);
        var buffer = Program.Get<System.IO.MemoryStream>(blob, "_buffer");
        if (buffer.Capacity != 0) throw new Exception("Announced total was allocated before receipt.");
        var chunk = new byte[chunkSize];
        chunk[0] = 42;
        blob.Append(total, chunk, 1);
        if (buffer.Capacity != chunkSize) throw new Exception("First chunk overallocated.");
        blob.Append(total, chunk, 2);
        blob.Append(total, new byte[] { 99 }, 3);
        if (buffer.Capacity != total) throw new Exception("Buffer grew past announced transfer size.");
        var result = blob.Complete();
        if (result.Length != total || result[0] != 42 || result[chunkSize] != 42 || result[total - 1] != 99)
            throw new Exception("Bounded growth corrupted transfer.");
        foreach (int invalid in new[] { 0, -1 })
        {
            bool rejected = false;
            try { new BlobReassembler(invalid, 0); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid announced size accepted.");
        }
    }

    public static void RejectBeforeGrowth()
    {
        var blob = new BlobReassembler(2, 10);
        blob.Append(2, new byte[] { 1 }, 20);
        bool rejected = false;
        try { blob.Append(2, new byte[] { 2, 3 }, 30); }
        catch (ProtocolException) { rejected = true; }
        if (!rejected || blob.ReceivedBytes != 1 || blob.ChunkCount != 1 || blob.LastChunkAtMs != 20)
            throw new Exception("Rejected chunk mutated the transfer.");
        blob.Append(2, new byte[] { 2 }, 40);
        var result = blob.Complete();
        if (result.Length != 2 || result[0] != 1 || result[1] != 2)
            throw new Exception("Valid transfer no longer completes.");
    }

    public static void RejectWireLengths()
    {
        foreach (int length in new[] { -1, ProtocolConstants.BlobChunkBytes + 1, int.MaxValue })
        {
            var writer = new NetworkWriter();
            writer.WriteString("map"); writer.WriteLong(1); writer.WriteInt(2);
            writer.WriteBool(true); writer.WriteInt(length);
            bool rejected = false;
            try { new BlobChunkMessage().Read(new NetworkReader(writer.ToArray())); }
            catch (ProtocolException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid chunk length accepted.");
        }
    }
}
