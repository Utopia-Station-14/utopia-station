namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationFirebreathComponent : Component
{
    [DataField]
    public float Cooldown = 25f;

    [DataField]
    public string ActionId = "ActionGeneticFireball";

    public TimeSpan NextUse = TimeSpan.Zero;

    public EntityUid? GrantedAction;
}
