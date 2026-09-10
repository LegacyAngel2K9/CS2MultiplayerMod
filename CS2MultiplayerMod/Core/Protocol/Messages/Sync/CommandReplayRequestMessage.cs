namespace CS2MultiplayerMod.Core.Protocol.Messages
{
    /// <summary>Client asks the host to replay a missing contiguous command range.</summary>
    public sealed class CommandReplayRequestMessage : INetMessage
    {
        public long FromSequence;
        public long ToSequence;

        public CommandReplayRequestMessage() { }
        public CommandReplayRequestMessage(long fromSequence, long toSequence)
        {
            FromSequence = fromSequence;
            ToSequence = toSequence;
        }

        public MessageType Type => MessageType.CommandReplayRequest;

        public void Write(NetworkWriter writer)
        {
            writer.WriteLong(FromSequence);
            writer.WriteLong(ToSequence);
        }

        public void Read(NetworkReader reader)
        {
            FromSequence = reader.ReadLong();
            ToSequence = reader.ReadLong();
        }
    }
}
