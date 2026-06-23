using Content.Shared.Construction.Prototypes;
using Content.Shared.Kitchen.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Kitchen.Components;

/// <summary>
/// The combo reagent grinder/juicer. The reason why grinding and juicing are seperate is simple,
/// think of grinding as a utility to break an object down into its reagents. Think of juicing as
/// converting something into its single juice form. E.g, grind an apple and get the nutriment and sugar
/// it contained, juice an apple and get "apple juice".
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedReagentGrinderSystem))]
public sealed partial class ReagentGrinderComponent : Component
{
    [DataField, AutoNetworkedField]
    public int StorageMaxEntities = 6;

    [DataField, AutoNetworkedField]
    public TimeSpan WorkTime = TimeSpan.FromSeconds(3.5); // Roughly matches the grind/juice sounds.

    [DataField, AutoNetworkedField]
    public float WorkTimeMultiplier = 1;

    [DataField]
    public SoundSpecifier ClickSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [DataField]
    public SoundSpecifier GrindSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/blender.ogg");

    [DataField]
    public SoundSpecifier JuiceSound { get; set; } = new SoundPathSpecifier("/Audio/Machines/juicer.ogg");

    [DataField, AutoNetworkedField]
    public GrinderAutoMode AutoMode = GrinderAutoMode.Off;

    public EntityUid? AudioStream;

    // Utopia-Tweak : Machine Parts
    [DataField]
    public int BaseStorageMaxEntities = 4;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
    public string MachinePartStorageMax = "MatterBin";

    [DataField]
    public int StoragePerPartTier = 4;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
    public string MachinePartWorkTime = "Manipulator";

    [DataField]
    public float PartTierWorkTimerMulitplier = 0.6f;
    // Utopia-Tweak : Machine Parts
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedReagentGrinderSystem))]
public sealed partial class ActiveReagentGrinderComponent : Component
{
    /// <summary>
    /// Remaining time until the grinder finishes grinding/juicing.
    /// </summary>
    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public GrinderProgram Program;
}

