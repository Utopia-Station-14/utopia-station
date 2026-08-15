using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Walls;

/// <summary>
/// Makes an entity automatically connect to adjacent entities
/// with the same component.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WallConnectComponent : Component
{
    /// <summary>
    /// Prefix of RSI state.
    /// Example:
    /// wall-0
    /// wall-1
    /// wall-2
    /// ...
    /// </summary>
    [DataField]
    public string StatePrefix = "wall-";

    /// <summary>
    /// Allows connecting only to walls with the same key.
    /// </summary>
    [DataField]
    public string ConnectKey = "wall";
}

/// <summary>
/// Appearance data keys used to pass the computed connection bitmask
/// (and whether it should be mirrored horizontally to save on art)
/// from server to client for <see cref="WallConnectComponent"/>.
/// </summary>
[Serializable, NetSerializable]
public enum WallVisuals : byte
{
    /// <summary>
    /// int - the *canonical* (already de-mirrored) bitmask of
    /// N/E/S/W/NE/SE/SW/NW connections. See WallConnectSystem.CalculateMask
    /// and WallConnectSystem.MirrorMaskHorizontal.
    /// </summary>
    ConnectMask,

    /// <summary>
    /// bool - whether the sprite layer should be flipped horizontally
    /// (X-scale of -1) to represent the actual (non-canonical) connection
    /// shape using the canonical state's art.
    /// </summary>
    FlipX,
}
