using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Sprite;

/// <summary>
/// If your client entity is behind this then the sprite's alpha will be lowered so your entity remains visible.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WindowSpriteComponent : Component
{
    public float Alpha = 0.6f;
}
