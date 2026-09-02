using Content.Shared.Atmos;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[Prototype("supermatterGasData")]
public sealed partial class SupermatterGasDataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Gas TargetGas;

    [DataField]
    public float TemperatureScaleModificator;

    [DataField]
    public float TemperatureProtectionModificator;

    [DataField]
    public float EnergyScaleModificator;

    [DataField]
    public float WasteOutputModificator;
}
