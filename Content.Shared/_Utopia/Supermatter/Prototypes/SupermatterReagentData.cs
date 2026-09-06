using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[Prototype("supermatterReagentData")]
public sealed partial class SupermatterReagentDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string TargetReagent = default!;

    [DataField]
    public float TemperatureScaleModificator;

    [DataField]
    public float TemperatureProtectionModificator;

    [DataField]
    public float EnergyScaleModificator;

    [DataField]
    public float WasteOutputModificator;
}
