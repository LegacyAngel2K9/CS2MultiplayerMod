namespace CS2MPMod.Core.Protocol.Messages
{
    /// <summary>Host response, queued after a complete replay, or a bounded recovery refusal.</summary>
    public sealed class CommandReplayResultMessage : INetMessage
    {
        public long Epoch;
        public long FromSequence;
        public long ToSequence;
        public bool Available;

        public MessageType Type => MessageType.CommandReplayResult;

        public void Write(NetworkWriter writer)
        {
            writer.WriteLong(Epoch);
            writer.WriteLong(FromSequence);
            writer.WriteLong(ToSequence);
            writer.WriteBool(Available);
        }

        public void Read(NetworkReader reader)
        {
            Epoch = reader.ReadLong();
            FromSequence = reader.ReadLong();
            ToSequence = reader.ReadLong();
            Available = reader.ReadBool();
        }
    }
}
