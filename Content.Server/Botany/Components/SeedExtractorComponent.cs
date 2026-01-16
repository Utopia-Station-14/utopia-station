using Content.Server.Botany.Systems;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Botany.Components;

[RegisterComponent]
[Access(typeof(SeedExtractorSystem))]
public sealed partial class SeedExtractorComponent : Component
{
    /// <summary>
    /// The minimum amount of seed packets dropped.
    /// </summary>
    [DataField("baseMinSeeds"), ViewVariables(VVAccess.ReadWrite)]
    public int BaseMinSeeds = 1;

    /// <summary>
    /// The maximum amount of seed packets dropped.
    /// </summary>
    [DataField("baseMaxSeeds"), ViewVariables(VVAccess.ReadWrite)]
    public int BaseMaxSeeds = 3;

    // Utopia-Tweak : Machine Parts
    /// <summary>
    /// Modifier to the amount of seeds outputted, set on <see cref="RefreshPartsEvent"/>.
    /// </summary>
    [DataField]
    public float SeedAmountMultiplier;

    /// <summary>
    /// Machine part whose tier modifies the amount of seed packets dropped.
    /// </summary>
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
    public string MachinePartSeedAmount = "Manipulator";

    /// <summary>
    /// How much the machine part quality affects the amount of seeds outputted.
    /// Going up a tier will multiply the seed output by this amount.
    /// </summary>
    [DataField]
    public float PartTierSeedAmountMultiplier = 1.5f;
    // Utopia-Tweak : Machine Parts
}
