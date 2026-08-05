namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationRegenerationComponent : Component
{
    [DataField]
    public float HealAmount = 1.0f;

    [DataField]
    public float Interval = 1.0f;

    [ViewVariables]
    public TimeSpan NextHeal;
}
