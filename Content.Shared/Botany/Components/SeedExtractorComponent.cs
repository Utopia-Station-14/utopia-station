using Content.Shared.Botany.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Destructible.Thresholds;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Botany.Components;

/// <summary>
/// Component for a machine for extracting seeds from plant produce.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(SeedExtractorSystem))]
public sealed partial class SeedExtractorComponent : Component
{
    /// <summary>
    /// The base amount of seed packets dropped.
    /// </summary>
    [DataField, AutoNetworkedField]
    public MinMax BaseSeeds = new(1, 3);

    // Utopia-Tweak : Machine Parts
    /// <summary>
    /// Modifier to the amount of seeds outputted, set on <see cref="RefreshPartsEvent"/>.
    /// </summary>
    [DataField]
    public float SeedAmountMultiplier;

    /// <summary>
    /// Machine part whose tier modifies the amount of seed packets dropped.
    /// </summary>
    [DataField]
    public ProtoId<MachinePartPrototype> MachinePartSeedAmount = "Manipulator";

    /// <summary>
    /// How much the machine part quality affects the amount of seeds outputted.
    /// Going up a tier will multiply the seed output by this amount.
    /// </summary>
    [DataField]
    public float PartTierSeedAmountMultiplier = 1.5f;
    // Utopia-Tweak : Machine Parts
}
