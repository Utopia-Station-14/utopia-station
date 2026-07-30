using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ZLevels.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelLadderComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool AllowUp = true;

    [DataField, AutoNetworkedField]
    public bool AllowDown = true;
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

[Serializable, NetSerializable]
public sealed class ZLevelLadderBuiState : BoundUserInterfaceState
{
    public List<ZMoveDirection> Directions;

    public ZLevelLadderBuiState(List<ZMoveDirection> directions)
    {
        Directions = directions;
    }
}