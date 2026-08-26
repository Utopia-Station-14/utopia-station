using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Supermatter;

[Prototype]
public sealed partial class SupermatterReagentDataPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Reagent { get; private set; } = default!;

    [DataField] public float TemperatureScaleModificator = 1f;
    [DataField] public float TemperatureProtectionModificator = 1f;
    [DataField] public float EnergyScaleModificator = 1f;
    [DataField] public float WasteOutputModificator = 1f;
}

[Prototype]
public sealed partial class SupermatterReagentReactionPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public Dictionary<string, float> Composition = new();
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
public record struct SupermatterReagentReactionEvent(
    SupermatterReagentReactionPrototype Reaction,
    float FrameTime
);