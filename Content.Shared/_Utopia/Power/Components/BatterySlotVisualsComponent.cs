using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Power.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class BatterySlotVisualsComponent : Component
{
    [DataField]
    public string? BaseState;

    [DataField]
    public int Steps = 4;

    [DataField]
    public bool ZeroVisible = true;
}

[Serializable, NetSerializable]
public enum BatterySlotVisuals : byte
{
    Battery,
    MaxCharge,
    CurrentCharge
}

public enum BatterySlotVisualsLayers : byte
{
    Charge,
}
