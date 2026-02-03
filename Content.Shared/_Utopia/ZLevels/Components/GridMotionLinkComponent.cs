using Robust.Shared.GameObjects;

namespace Content.Shared._Utopia.ZLevels.Components;

[RegisterComponent]
public sealed partial class GridMotionLinkComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string GroupId = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsSource = false;
}
