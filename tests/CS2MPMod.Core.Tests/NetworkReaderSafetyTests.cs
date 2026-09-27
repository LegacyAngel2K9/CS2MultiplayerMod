using System;
using CS2MPMod.Core.Protocol;

internal static class NetworkReaderSafetyTests
{
    public static void InvalidSlices()
    {
        foreach (var slice in new[] { (-1, 1), (0, -1), (5, 0), (1, 4), (1, int.MaxValue), (int.MaxValue, 1) })
        {
            bool rejected = false;
            try { new NetworkReader(new byte[4], slice.Item1, slice.Item2); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            if (!rejected) throw new Exception("Invalid reader slice accepted.");
        }
    }

    public static void SliceIsolation()
    {
        var reader = new NetworkReader(new byte[] { 9, 1, 2, 8 }, 1, 2);
        if (reader.Remaining != 2 || reader.ReadShort() != 513 || reader.Remaining != 0)
            throw new Exception("Valid slice decoded incorrectly.");
        ExpectProtocolFailure(() => reader.ReadByte());
        var empty = new NetworkReader(new byte[4], 4, 0);
        if (empty.ReadBytes(0).Length != 0) throw new Exception("Empty slice failed.");
        ExpectProtocolFailure(() => empty.ReadByte());
        reader = new NetworkReader(new byte[] { 7, 8 });
        ExpectProtocolFailure(() => reader.ReadBytes(int.MaxValue));
        ExpectProtocolFailure(() => reader.ReadBytes(-1));
        if (reader.Remaining != 2 || reader.ReadByte() != 7)
            throw new Exception("Rejected read consumed bytes.");
    }

    private static void ExpectProtocolFailure(Action action)
    {
        try { action(); }
        catch (ProtocolException) { return; }
        throw new Exception("Out-of-bounds read accepted.");
    }
}
