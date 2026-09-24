using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[Prototype]
public sealed partial class SupermatterReagentDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string TargetReagent = default!;

    [DataField]
    public float TemperatureScaleModifier;

    [DataField]
    public float TemperatureProtectionModifier;

    [DataField]
    public float EnergyScaleModifier;

    [DataField]
    public float WasteOutputModifier;
}
