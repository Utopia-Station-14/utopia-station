using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ZLevels.Moving.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UtopiaZLevelsMovingComponent : Component
{
    [DataField]
    public EntProtoId Action = "UtopiaActionZLevelsMoving";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public EntProtoId DownAction = "UtopiaActionZLevelsMovingDown";

    [DataField, AutoNetworkedField]
    public EntityUid? DownActionEntity;

    [DataField]
    public float AnchoredDelay = 2f;

    [DataField]
    public float FloatingDelay = 1.5f;

    [DataField]
    public float DownDelay = 1.5f;

    [DataField]
    public float StructureRange = 1f;
}

public sealed partial class UtopiaZLevelsMovingActionEvent : InstantActionEvent;
public sealed partial class UtopiaZLevelsMovingDownActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class UtopiaZLevelsMovingDoAfterEvent : DoAfterEvent
{
    public bool Anchored;
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class UtopiaZLevelsMovingDownDoAfterEvent : SimpleDoAfterEvent;
