using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Walls;

[RegisterComponent, NetworkedComponent]
public sealed partial class WallConnectComponent : Component
{
    [DataField]
    public bool UpdateSelf = true;

    [DataField]
    public string StatePrefix = "wall-";

    [DataField]
    public string Start = "wall-";

    [DataField]
    public List<string> ConnectKey = new() { "wall" };
}

[Serializable, NetSerializable]
public enum WallVisuals : byte
{
    ConnectMask,
    FlipX,
}
