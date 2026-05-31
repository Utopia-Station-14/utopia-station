namespace Content.Server._Utopia.Genetics.Mutations.Components;

/// <summary>
/// Mutation component that has a chance to apply toxin damage at regular intervals.
/// </summary>
[RegisterComponent]
public sealed partial class MutationBloodToxificationComponent : Component
{
    [DataField]
    public float Interval = 4.0f;

    [DataField]
    public float Chance = 0.25f;

    [DataField]
    public float ToxinAmount = 1.0f;

    [DataField]
    public string DamageType = "Poison";

    [ViewVariables]
    public TimeSpan NextTick;
}
