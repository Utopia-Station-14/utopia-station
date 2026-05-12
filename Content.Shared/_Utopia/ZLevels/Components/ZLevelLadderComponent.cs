using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ZLevels.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelLadderComponent : Component
{
    [DataField]
    public bool AllowUp = true;

    [DataField]
    public bool AllowDown = true;

    [DataField, AutoNetworkedField]
    public List<ZMoveDirection> Directions = new();
}

[Serializable, NetSerializable]
public enum ZMoveDirection : byte
{
    Up,
    Down
}

[Serializable, NetSerializable]
public enum ZLevelLadderUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ZLevelLadderMessage(ZMoveDirection direction) : BoundUserInterfaceMessage
{
    public ZMoveDirection Direction = direction;
}
