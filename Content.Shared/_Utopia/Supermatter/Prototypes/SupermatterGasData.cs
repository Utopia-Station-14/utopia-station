using Content.Shared.Atmos;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[Prototype]
public sealed partial class SupermatterGasDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Gas TargetGas;

    [DataField]
    public float TemperatureScaleModifier;

    [DataField]
    public float TemperatureProtectionModifier;

    [DataField]
    public float EnergyScaleModifier;

    [DataField]
    public float WasteOutputModifier;
}
