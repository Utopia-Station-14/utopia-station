using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[Prototype("supermatterReagentReaction")]
public sealed partial class SupermatterReagentReactionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<string, float> Composition = new();

    [DataField]
    public float Tolerance = 0.05f;

    [DataField]
    public Vector4 ModifiersVector = Vector4.One;

    [DataField]
    public List<SupermatterReagentReactionEffect> Effects = new();
}
