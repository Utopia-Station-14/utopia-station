using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Walls;

[RegisterComponent, NetworkedComponent]
public sealed partial class WallConnectComponent : Component
{
    [DataField]
    public string StatePrefix = "wall-";

    [DataField]
    public string ConnectKey = "wall";
}

[Serializable, NetSerializable]
public enum WallVisuals : byte
{
    ConnectMask,
    FlipX,
}
