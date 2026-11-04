using Robust.Shared.GameStates;

namespace Content.Shared.PortalView;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PortalViewComponent : Component
{
    [DataField, AutoNetworkedField]
    public float HalfSize = 0.5f;
}
