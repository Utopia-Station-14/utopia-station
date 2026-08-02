using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Combat;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CombatModePickupWhitelistComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;
}
