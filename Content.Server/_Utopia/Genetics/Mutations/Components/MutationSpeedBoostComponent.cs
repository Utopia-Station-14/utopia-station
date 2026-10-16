namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationSpeedBoostComponent : Component
{
    [DataField(required: true)]
    public float WalkMultiplier = 1.0f;

    [DataField(required: true)]
    public float SprintMultiplier = 1.0f;
}
