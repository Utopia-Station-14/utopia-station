using Content.Shared.Damage;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;

namespace Content.Shared.Weapons.Melee.MeleeBlacklist;

public sealed partial class MeleeBlacklistSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    [SubscribeLocalEvent]
    private void OnMeleeHit(Entity<MeleeBlacklistComponent> ent, ref AttemptMeleeEvent args)
    {
        if (_whitelist.IsWhitelistPassOrNull(ent.Comp.Blacklist, args.User))
        {
            args.Cancelled = true;
        }
        else if (_whitelist.IsWhitelistFail(ent.Comp.Whitelist, args.User))
        {
            args.Cancelled = true;
        }
    }
}