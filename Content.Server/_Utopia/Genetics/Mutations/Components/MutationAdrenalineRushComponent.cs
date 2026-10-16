using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationAdrenalineRushComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ActionId = "ActionAdrenalineRush";

    [DataField]
    public string ReagentId = "Epinephrine";

    [DataField]
    public float Amount = 10f;

    public EntityUid? GrantedAction;
}
