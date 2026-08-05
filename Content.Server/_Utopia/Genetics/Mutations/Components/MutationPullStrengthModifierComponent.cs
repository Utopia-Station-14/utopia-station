namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationPullStrengthModifierComponent : Component
{
    [DataField("multiplier", required: true)]
    public float PullSlowdownMultiplier = 1.0f;
}
