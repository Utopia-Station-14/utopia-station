using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationOpticEnergizerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId ActionId = "ActionOpticBlast";

    public EntityUid? GrantedAction;

    [DataField]
    public EntProtoId LaserProto = "RedMediumLaser";
}
