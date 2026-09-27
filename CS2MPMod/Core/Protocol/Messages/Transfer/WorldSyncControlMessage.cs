namespace CS2MPMod.Core.Protocol.Messages
{
    /// <summary>Stages in one host-authoritative, epoch-scoped world replacement.</summary>
    public enum WorldSyncStage : byte
    {
        Begin = 1,
        Quiesced = 2,
        Loaded = 3,
        Failed = 4,
        Resume = 5,
        Abort = 6,
    }

    /// <summary>
    /// Control plane for a world snapshot. Begin/Resume/Abort flow host to client;
    /// Quiesced/Loaded/Failed flow client to host. The epoch prevents stale controls or
    /// chunks from a superseded transfer affecting the current world.
    /// </summary>
    public sealed class WorldSyncControlMessage : INetMessage
    {
        public long Epoch;
        public WorldSyncStage Stage;
        public float ResumeSpeed;
        /// <summary>Resume: all commands through this sequence are superseded by the installed snapshot.</summary>
        public long CommandSequence;

        public WorldSyncControlMessage() { }

        public WorldSyncControlMessage(long epoch, WorldSyncStage stage, float resumeSpeed = 0f,
            long commandSequence = 0)
        {
            Epoch = epoch;
            Stage = stage;
            ResumeSpeed = resumeSpeed;
            CommandSequence = commandSequence;
        }

        public MessageType Type => MessageType.WorldSyncControl;

        public void Write(NetworkWriter writer)
        {
            writer.WriteLong(Epoch);
            writer.WriteByte((byte)Stage);
            writer.WriteFloat(ResumeSpeed);
            writer.WriteLong(CommandSequence);
        }

        public void Read(NetworkReader reader)
        {
            Epoch = reader.ReadLong();
            Stage = (WorldSyncStage)reader.ReadByte();
            ResumeSpeed = reader.ReadFloat();
            CommandSequence = reader.ReadLong();
        }
    }
}
