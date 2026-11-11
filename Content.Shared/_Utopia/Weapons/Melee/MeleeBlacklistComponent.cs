using Content.Shared.Whitelist;

namespace Content.Shared.Weapons.Melee.MeleeBlacklist;

[RegisterComponent]
public sealed partial class MeleeBlacklistComponent : Component
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;
}
