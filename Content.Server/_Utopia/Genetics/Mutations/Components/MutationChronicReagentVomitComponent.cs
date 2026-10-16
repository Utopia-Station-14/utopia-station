namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationChronicReagentVomitComponent : Component
{
    [DataField(required: true)]
    public string Reagent = default!;

    [DataField]
    public int MinAmount = 5;

    [DataField]
    public int MaxAmount = 10;

    [DataField]
    public float MinInterval = 120f;

    [DataField]
    public float MaxInterval = 300f;

    [DataField]
    public float Chance = 0.6f;

    [DataField]
    public TimeSpan NextVomitTime;
}
