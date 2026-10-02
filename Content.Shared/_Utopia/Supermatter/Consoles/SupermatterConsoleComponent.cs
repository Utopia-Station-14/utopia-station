using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Supermatter.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SupermatterConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? FocusSupermatter;

    [DataField]
    public float UpdateInterval = 1f;

    [ViewVariables]
    public TimeSpan NextUpdateTime;
}
