using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Construction.Components
{
    [RegisterComponent, ComponentProtoName("Computer")]
    public sealed partial class ComputerComponent : Component
    {
        [DataField("board", customTypeSerializer:typeof(PrototypeIdSerializer<EntityPrototype>))]
        public string? BoardPrototype;
    }

    // Utopia-Tweak : Machine Parts
    public enum MachineUpgradeScalingType : byte
    {
        Linear,
        Exponential
    }
    // Utopia-Tweak : Machine Parts
}
