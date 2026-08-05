namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationIncreaseStaminaCritThresholdComponent : Component
{
    [DataField]
    public float ThresholdBonus = 30f;  // +30 = 130 threshold
}
