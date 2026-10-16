using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared.Atmos
{
    [NetSerializable, Serializable]
    public enum GasTankMixerUiKey : byte
    {
        Key
    }

    [NetSerializable, Serializable]
    public sealed class GasTankMixerBoundUserInterfaceState : BoundUserInterfaceState
    {
        public bool HasTankA { get; }
        public bool HasTankB { get; }
        public float Timer { get; }
        public bool IsActive { get; }

        public GasTankMixerBoundUserInterfaceState(bool hasTankA, bool hasTankB, float timer, bool isActive)
        {
            HasTankA = hasTankA;
            HasTankB = hasTankB;
            Timer = timer;
            IsActive = isActive;
        }
    }

    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
    public sealed partial class GasTankMixerVisualsComponent : Component
    {
        [AutoNetworkedField]
        public bool HasTankA;

        [AutoNetworkedField]
        public bool HasTankB;
    }

    public enum GasTankMixerVisualLayers : byte
    {
        TankA,
        TankB
    }

    [NetSerializable, Serializable]
    public sealed class GasTankMixerStartMessage : BoundUserInterfaceMessage { }

    [NetSerializable, Serializable]
    public sealed class GasTankMixerSetTimeMessage : BoundUserInterfaceMessage
    {
        public float Time { get; }
        public GasTankMixerSetTimeMessage(float time) => Time = time;
    }

    [NetSerializable, Serializable]
    public sealed class GasTankMixerEjectMessage : BoundUserInterfaceMessage
    {
        public string SlotId { get; }
        public GasTankMixerEjectMessage(string slotId) => SlotId = slotId;
    }
}