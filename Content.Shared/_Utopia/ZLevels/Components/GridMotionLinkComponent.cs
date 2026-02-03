using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.ZLevels.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridMotionLinkComponent : Component
{
    [DataField, AutoNetworkedField]
    public string GroupId = string.Empty;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool IsSource = false;
}
