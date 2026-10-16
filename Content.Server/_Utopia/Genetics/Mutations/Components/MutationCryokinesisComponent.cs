namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationCryokinesisComponent : Component
{
    [DataField]
    public float Cooldown = 25f;

    [DataField]
    public string ActionId = "ActionGeneticIceball";

    public TimeSpan NextUse = TimeSpan.Zero;

    public EntityUid? GrantedAction;
}
