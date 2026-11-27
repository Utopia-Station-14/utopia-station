using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Portals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PortalViewComponent : Component
{
    [DataField, AutoNetworkedField]
    public float HalfSize = 0.5f;
}
