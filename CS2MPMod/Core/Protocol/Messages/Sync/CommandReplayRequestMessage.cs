namespace CS2MPMod.Core.Protocol.Messages
{
    /// <summary>Client asks the host to replay a missing contiguous command range.</summary>
    public sealed class CommandReplayRequestMessage : INetMessage
    {
        public long FromSequence;
        public long ToSequence;
        public long Epoch;

        public CommandReplayRequestMessage() { }
        public CommandReplayRequestMessage(long fromSequence, long toSequence, long epoch = 0)
        {
            FromSequence = fromSequence;
            ToSequence = toSequence;
            Epoch = epoch;
        }

        public MessageType Type => MessageType.CommandReplayRequest;

        public void Write(NetworkWriter writer)
        {
            writer.WriteLong(FromSequence);
            writer.WriteLong(ToSequence);
            writer.WriteLong(Epoch);
        }

        public void Read(NetworkReader reader)
        {
            FromSequence = reader.ReadLong();
            ToSequence = reader.ReadLong();
            Epoch = reader.ReadLong();
        }
    }
}
