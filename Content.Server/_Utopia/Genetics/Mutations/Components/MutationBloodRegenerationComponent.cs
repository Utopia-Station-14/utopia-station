namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationBloodRegenerationComponent : Component
{
    [DataField]
    public float RegenRatePerSecond = 2.0f;

    [DataField]
    public float TargetPercentage = 1.0f;
}
