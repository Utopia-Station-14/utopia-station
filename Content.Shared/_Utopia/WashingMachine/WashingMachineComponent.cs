using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.WashingMachine;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WashingMachineComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan WashTime = TimeSpan.FromSeconds(5);

    [AutoNetworkedField]
    public TimeSpan? WashFinishTime;

    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(6);

    [AutoNetworkedField]
    public TimeSpan NextWashAllowed;

    [DataField]
    public SoundSpecifier? WashLoopSound;

    [DataField]
    public SoundSpecifier? WashFinishedSound;

    [DataField, AutoNetworkedField]
    public WashingMachineState State = WashingMachineState.Idle;

    public EntityUid? AudioStream;

    [DataField]
    public float BluntDamagePerSecond = 6.0f;

    [DataField]
    public float ThumpSoundChance = 0.8f;

    [DataField]
    public string WaterSprayReagent = "Water";

    [DataField]
    public float WaterSprayAmount = 150.0f;

    [DataField]
    public float WaterSprayChance = 1.0f;

    [DataField]
    public float SelfDamagePerSecond = 6.0f;

    [ViewVariables]
    public float AccumulatedSelfDamage = 0f;
}

[Serializable, NetSerializable]
public enum WashingMachineState : byte
{
    Idle,
    Washing,
    Broken
}

[Serializable, NetSerializable]
public enum WashingMachineVisuals : byte
{
    State
}
