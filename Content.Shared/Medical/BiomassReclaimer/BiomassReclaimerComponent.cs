using Content.Shared.Construction.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.BiomassReclaimer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BiomassReclaimerSystem))]
public sealed partial class BiomassReclaimerComponent : Component
{
    /// <summary>
    /// The sound played when processing starts or resumes after a power loss.
    /// </summary>
    [DataField]
    public SoundSpecifier? StartupSound = new SoundPathSpecifier("/Audio/Machines/reclaimer_startup.ogg");

    /// <summary>
    /// The material produced when processing an entity.
    /// </summary>
    [DataField]
    public ProtoId<MaterialPrototype> OutputMaterial = "Biomass";

    /// <summary>
    /// The interval between attempts to spill blood or throw an item.
    /// </summary>
    [DataField]
    public TimeSpan RandomMessInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The chance to spill blood at each mess interval.
    /// </summary>
    [DataField]
    public float BloodSpillChance = 0.2f;

    /// <summary>
    /// The chance to throw an item at each mess interval.
    /// </summary>
    [DataField]
    public float ItemThrowChance = 0.03f;

    /// <summary>
    /// The minimum speed of thrown items, inclusive.
    /// </summary>
    [DataField]
    public float ItemThrowMinSpeed = 1f;

    /// <summary>
    /// The maximum speed of thrown items, exclusive.
    /// </summary>
    [DataField]
    public float ItemThrowMaxSpeed = 10f;

    /// <summary>
    /// The jitter amplitude while processing.
    /// </summary>
    [DataField]
    public float JitterAmplitude = -10f;

    /// <summary>
    /// The jitter frequency while processing.
    /// </summary>
    [DataField]
    public float JitterFrequency = 100f;

    /// <summary>
    /// How much blood can be spilled while processing a mob.
    /// </summary>
    [DataField]
    public FixedPoint2 BloodSpillVolume = 50;

    /// <summary>
    /// Non-integer biomass left over from processing, added to the next yield.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float YieldRemainder;

    /// <summary>
    /// How many units of biomass it produces for each unit of mass.
    /// </summary>
    [DataField]
    public float YieldPerUnitMass = 0.4f;

    /// <summary>
    /// How many seconds to take to insert an entity per unit of its mass.
    /// </summary>
    [DataField]
    public float BaseInsertionDelay = 0.1f;

    /// <summary>
    /// How much to multiply biomass yield from botany produce.
    /// </summary>
    [DataField]
    public float ProduceYieldMultiplier = 0.25f;

    /// <summary>
    /// The time it takes to process a mob, per mass.
    /// </summary>
    [DataField]
    public float ProcessingTimePerUnitMass = 0.5f;

    /// <summary>
    /// Will this refuse to gib a living mob?
    /// </summary>
    [DataField]
    public bool SafetyEnabled = true;

    // Utopia-Tweak : Machine Parts
    /// <summary>
    /// The base yield per mass unit when no components are upgraded.
    /// </summary>
    [DataField]
    public float BaseYieldPerUnitMass = 0.4f;

    /// <summary>
    /// Machine part whose tier modifies the yield per mass.
    /// </summary>
    [DataField]
    public ProtoId<MachinePartPrototype> MachinePartYieldAmount = "Manipulator";

    /// <summary>
    /// How much the machine part quality affects the yield.
    /// Going up a tier will multiply the yield by this amount.
    /// </summary>
    [DataField]
    public float PartTierYieldAmountMultiplier = 1.25f;

    /// <summary>
    /// The base time per mass unit that it takes to process a mob
    /// when no components are upgraded.
    /// </summary>
    [DataField]
    public float BaseProcessingTimePerUnitMass = 0.5f;

    /// <summary>
    /// The machine part that increses the processing speed.
    /// </summary>
    [DataField]
    public ProtoId<MachinePartPrototype> MachinePartProcessingSpeed = "Laser";

    /// <summary>
    /// How much the machine part quality affects the yield.
    /// Going up a tier will multiply the speed by this amount.
    /// </summary>
    [DataField]
    public float PartTierSpeedMultiplier = 1.35f;
    // Utopia-Tweak : Machine Parts
}
