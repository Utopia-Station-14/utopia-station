using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Supermatter;

[Prototype]
public sealed partial class SupermatterGasDataPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)] public Gas Gas { get; private set; }

    [DataField] public float TemperatureScaleModificator = 1f;
    [DataField] public float TemperatureProtectionModificator = 1f;
    [DataField] public float EnergyScaleModificator = 1f;
    [DataField] public float WasteOutputModificator = 1f;
}

[Prototype]
public sealed partial class SupermatterReactionPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField(required: true)] public Dictionary<Gas, float> Composition = new();
    [DataField] public float Tolerance = 0.05f;

    [DataField] public float TemperatureScaleMultiplier = 1f;
    [DataField] public float TemperatureProtectionMultiplier = 1f;
    [DataField] public float EnergyScaleMultiplier = 1f;
    [DataField] public float WasteOutputMultiplier = 1f;

    public Vector4 ModifiersVector => new(
        TemperatureScaleMultiplier,
        TemperatureProtectionMultiplier,
        EnergyScaleMultiplier,
        WasteOutputMultiplier
    );
}

[ByRefEvent]
public record struct SupermatterReactionEvent(
    SupermatterReactionPrototype Reaction,
    GasMixture Mixture,
    float FrameTime
);
