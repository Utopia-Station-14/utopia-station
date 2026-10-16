namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationStupefactionComponent : Component
{
    [DataField]
    public float MinInterval = 120f;

    [DataField]
    public float MaxInterval = 180f;

    [DataField]
    public float DrainAmount = 999f;

    [ViewVariables]
    public TimeSpan NextDrainTime;
}
