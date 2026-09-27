namespace CS2MPMod.Core.Protocol.Messages
{
    /// <summary>
    /// Transport envelope for a simulation command (see
    /// <see cref="Sync.ISimulationCommand"/>). The core never interprets the body; it
    /// carries the command id, the simulation tick the command is scheduled for, and
    /// the opaque serialized command bytes. The game layer encodes/decodes the body.
    ///
    /// Tick describes the origin's simulation frame, not a lockstep guarantee.
    /// Epoch and Sequence identify the authoritative stream across snapshot installs.
    /// </summary>
    public sealed class SimulationCommandMessage : INetMessage
    {
        public int OriginPlayerId;
        public long Tick;
        /// <summary>Monotonic host order for this command. Zero means not host-stamped yet.</summary>
        public long Sequence;
        public long Epoch;
        public ushort CommandId;
        public byte[] Body;

        public SimulationCommandMessage() { }

        public SimulationCommandMessage(int originPlayerId, long tick, long sequence,
            ushort commandId, byte[] body, long epoch = 0)
        {
            OriginPlayerId = originPlayerId;
            Tick = tick;
            Sequence = sequence;
            Epoch = epoch;
            CommandId = commandId;
            Body = body ?? System.Array.Empty<byte>();
        }

        public MessageType Type => MessageType.SimulationCommand;

        public void Write(NetworkWriter writer)
        {
            writer.WriteInt(OriginPlayerId);
            writer.WriteLong(Tick);
            writer.WriteLong(Sequence);
            writer.WriteLong(Epoch);
            writer.WriteShort((short)CommandId);
            writer.WriteInt(Body != null ? Body.Length : 0);
            if (Body != null && Body.Length > 0)
                writer.WriteBytes(Body, 0, Body.Length);
        }

        public void Read(NetworkReader reader)
        {
            OriginPlayerId = reader.ReadInt();
            Tick = reader.ReadLong();
            Sequence = reader.ReadLong();
            Epoch = reader.ReadLong();
            CommandId = (ushort)reader.ReadShort();
            int length = reader.ReadInt();
            if (length < 0 || length != reader.Remaining)
                throw new ProtocolException("Command body length does not match its envelope.");
            Body = length > 0 ? reader.ReadBytes(length) : System.Array.Empty<byte>();
        }
    }
}
