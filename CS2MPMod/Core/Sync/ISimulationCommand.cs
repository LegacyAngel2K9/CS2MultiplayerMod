using CS2MPMod.Core.Protocol;

namespace CS2MPMod.Core.Sync
{
    public interface ISimulationCommand
    {
        /// <summary>Stable identifier used to route the payload back to its handler.</summary>
        ushort CommandId { get; }

        void Write(NetworkWriter writer);

        void Read(NetworkReader reader);
    }
}
