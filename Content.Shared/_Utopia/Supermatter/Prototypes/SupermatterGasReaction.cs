using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[Prototype("supermatterReaction")]
public sealed partial class SupermatterReactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<Gas, float> Composition = new();

    [DataField]
    public float Tolerance = 0.05f;

    [DataField]
    public Vector4 ModifiersVector = Vector4.One;

    [DataField]
    public List<SupermatterGasReactionEffect> Effects = new();
}
