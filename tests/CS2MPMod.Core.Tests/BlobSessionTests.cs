using System;
using System.Collections.Generic;
using CS2MPMod.Core.Protocol.Messages;
using CS2MPMod.Core.Session;

internal static class BlobSessionTests
{
    private sealed class EpochSink : SessionObserver
    {
        public int Calls;
        public long Epoch;
        public byte[] Data;
        public override void OnBlobReceived(string channel, long transferId, byte[] data)
        { Calls++; Epoch = transferId; Data = data; }
    }

    public static void SupersededSnapshotChunks()
    {
        var f = new Program.Fixture();
        var sink = new EpochSink();
        f.Session.AddObserver(sink);
        f.Session.AllowBlobChannel("map", 16);
        f.Receive(new WorldSyncControlMessage(1, WorldSyncStage.Begin));
        f.Receive(new BlobChunkMessage("map", 1L, 2, false, new byte[] { 1 }));
        f.Receive(new WorldSyncControlMessage(2, WorldSyncStage.Begin));
        f.Receive(new BlobChunkMessage("map", 2L, 2, false, new byte[] { 3 }));
        f.Receive(new BlobChunkMessage("map", 1L, 2, true, new byte[] { 2 }));
        if (sink.Calls != 0 || f.Session.IncomingBlobTransferId != 2 || f.Session.IncomingBlobReceived != 1)
            throw new Exception("Stale chunk disrupted the current snapshot.");
        f.Receive(new BlobChunkMessage("map", 2L, 2, true, new byte[] { 4 }));
        if (sink.Calls != 1 || sink.Epoch != 2 || sink.Data.Length != 2 || sink.Data[0] != 3 || sink.Data[1] != 4)
            throw new Exception("Superseded bytes leaked into new snapshot.");
        f.Receive(new WorldSyncControlMessage(2, WorldSyncStage.Resume));
        f.Receive(new BlobChunkMessage("map", 2L, 1, true, new byte[] { 9 }));
        if (sink.Calls != 1 || f.Session.IncomingBlobChannel != null)
            throw new Exception("Post-resume snapshot was delivered again.");
    }

    public static void AbortedSnapshotChunks()
    {
        var f = new Program.Fixture();
        var sink = new EpochSink();
        f.Session.AddObserver(sink);
        f.Session.AllowBlobChannel("map", 16);
        f.Receive(new WorldSyncControlMessage(1, WorldSyncStage.Begin));
        f.Receive(new BlobChunkMessage("map", 1L, 2, false, new byte[] { 1 }));
        f.Receive(new WorldSyncControlMessage(1, WorldSyncStage.Abort));
        f.Receive(new BlobChunkMessage("map", 1L, 2, true, new byte[] { 2 }));
        if (sink.Calls != 0 || f.Session.IncomingBlobChannel != null ||
            Program.Get<Dictionary<string, BlobReassembler>>(f.Session, "_blobs").Count != 0)
            throw new Exception("Aborted snapshot retained or delivered data.");
        f.Receive(new WorldSyncControlMessage(2, WorldSyncStage.Begin));
        f.Receive(new BlobChunkMessage("map", 2L, 1, true, new byte[] { 7 }));
        if (sink.Calls != 1 || sink.Epoch != 2 || sink.Data[0] != 7)
            throw new Exception("Abort prevented the next valid snapshot.");
    }

    public static void RejectionPreservesOtherProgress()
    {
        // Exercise both the early total-size rejection and Append's mid-transfer rejection.
        foreach (int invalidTotal in new[] { 17, 3 })
        {
            var f = new Program.Fixture();
            f.Session.AllowBlobChannel("map", 16);
            f.Session.AllowBlobChannel("other", 16);
            f.Receive(new BlobChunkMessage("map", 2, false, new byte[] { 1 }));
            f.Receive(new BlobChunkMessage("other", 2, false, new byte[] { 2 }));
            f.Receive(new BlobChunkMessage("map", invalidTotal, true, new byte[] { 3 }));
            var active = Program.Get<Dictionary<string, BlobReassembler>>(f.Session, "_blobs");
            var ids = Program.Get<Dictionary<string, long>>(f.Session, "_blobTransferIds");
            if (active.Count != 1 || !active.ContainsKey("other") || ids.Count != 1 ||
                f.Session.IncomingBlobChannel != "other" || f.Session.IncomingBlobReceived != 1 ||
                f.Session.IncomingBlobTotal != 2)
                throw new Exception("Rejected channel cleared unrelated progress or retained its transfer.");
            f.Receive(new BlobChunkMessage("other", 2, true, new byte[] { 4 }));
            if (active.Count != 0 || ids.Count != 0 || f.Session.IncomingBlobChannel != null)
                throw new Exception("Unaffected channel did not complete cleanly.");
        }
    }

    public static void ExpiryPreservesOtherProgress()
    {
        var f = new Program.Fixture();
        f.Session.AllowBlobChannel("map", 16);
        f.Session.AllowBlobChannel("other", 16);
        f.Receive(new BlobChunkMessage("map", 2, false, new byte[] { 1 })); // t=100
        f.Receive(new BlobChunkMessage("other", 2, false, new byte[] { 2 })); // t=101
        var sweep = typeof(MultiplayerSession).GetMethod("SweepStalledBlobs",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        sweep.Invoke(f.Session, new object[] { 60101L }); // map expired; other exactly at deadline
        var active = Program.Get<Dictionary<string, BlobReassembler>>(f.Session, "_blobs");
        var ids = Program.Get<Dictionary<string, long>>(f.Session, "_blobTransferIds");
        if (active.Count != 1 || !active.ContainsKey("other") || ids.Count != 1 || !ids.ContainsKey("other") ||
            f.Session.IncomingBlobChannel != "other" || f.Session.IncomingBlobReceived != 1 ||
            f.Session.IncomingBlobTotal != 2)
            throw new Exception("Expired transfer cleared unrelated live progress or retained its buffer.");
        sweep.Invoke(f.Session, new object[] { 65102L });
        if (active.Count != 0 || ids.Count != 0 || f.Session.IncomingBlobChannel != null ||
            f.Session.IncomingBlobReceived != 0 || f.Session.IncomingBlobTotal != 0)
            throw new Exception("Final stalled transfer was not fully cleared.");
    }

    private sealed class Sink : SessionObserver
    {
        public readonly List<byte[]> Payloads = new List<byte[]>();
        public override void OnBlobReceived(string channel, long transferId, byte[] data)
        {
            if (channel != "map" || transferId != 0) throw new Exception("Wrong transfer identity.");
            Payloads.Add(data);
        }
    }

    public static void InvalidThenValid()
    {
        var f = new Program.Fixture();
        var sink = new Sink();
        f.Session.AddObserver(sink);
        f.Session.AllowBlobChannel("map", 16);
        f.Receive(new BlobChunkMessage("map", 3, false, new byte[] { 1, 2 }));
        if (f.Session.IncomingBlobReceived != 2) throw new Exception("Progress missing.");
        f.Receive(new BlobChunkMessage("map", 3, true, new byte[] { 3, 4 }));
        if (sink.Payloads.Count != 0 || f.Session.IncomingBlobReceived != 0 ||
            Program.Get<Dictionary<string, BlobReassembler>>(f.Session, "_blobs").Count != 0)
            throw new Exception("Overflow reached observer or retained transfer.");
        f.Receive(new BlobChunkMessage("map", 2, false, new byte[] { 5 }));
        f.Receive(new BlobChunkMessage("map", 2, true, new byte[] { 6 }));
        if (sink.Payloads.Count != 1 || sink.Payloads[0].Length != 2 ||
            sink.Payloads[0][0] != 5 || sink.Payloads[0][1] != 6 || f.Session.IncomingBlobChannel != null)
            throw new Exception("Valid retry failed or retained progress.");
        f.Receive(new BlobChunkMessage("map", 1, true, new byte[] { 9 }));
        if (sink.Payloads.Count != 2 || sink.Payloads[0][0] != 5 || sink.Payloads[0][1] != 6)
            throw new Exception("Later transfer mutated earlier delivered data.");
    }

    public static void RejectUnapprovedTransfers()
    {
        var f = new Program.Fixture();
        var sink = new Sink();
        f.Session.AddObserver(sink);
        f.Session.AllowBlobChannel("map", 16);
        f.Receive(new BlobChunkMessage("unknown", 1, true, new byte[] { 1 }));
        f.Receive(new BlobChunkMessage("map", 17, true, new byte[] { 1 }));
        f.Receive(new BlobChunkMessage("map", 8L, 1, true, new byte[] { 1 }));
        f.Receive(new BlobChunkMessage("map", 2, true, new byte[] { 1 }));
        if (sink.Payloads.Count != 0 || f.Session.IncomingBlobChannel != null ||
            Program.Get<Dictionary<string, BlobReassembler>>(f.Session, "_blobs").Count != 0)
            throw new Exception("Unapproved or truncated transfer escaped validation.");
        var host = new Program.Fixture(SessionRole.Host);
        host.Session.AllowBlobChannel("map", 16);
        host.Session.AddObserver(sink);
        host.Receive(new BlobChunkMessage("map", 1, true, new byte[] { 1 }));
        if (sink.Payloads.Count != 0 || host.Transport.Disconnected.Count != 1)
            throw new Exception("Client upload was accepted by host.");
    }
}
