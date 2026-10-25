using Content.Server._Utopia.Sponsors;
using Content.Shared._Utopia.Sponsors;
using Robust.Server.Player;

public sealed partial class CheckSponsorSystem : EntitySystem
{
    [Dependency] private SponsorsManager _sponsorsManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CheckUserSponsor>(SetSponsorStatus);
    }

    public bool CheckUser(string player)
    {
        EntityUid uid = EntityUid.Parse(player);
        _playerManager.TryGetSessionByEntity(uid, out var session);

        SponsorInfo? sponsorData;
        if (session != null)
            _sponsorsManager.TryGetInfo(session.UserId, out sponsorData);
        else
            return false;

        if (sponsorData != null && (sponsorData.Tier > 0 || sponsorData.AllowJob))
            return true;
        else
            return false;
    }

    private void SetSponsorStatus(CheckUserSponsor ev)
    {
        var isSponsor = CheckUser(ev.Player);
        var eve = new CheckedUserSponsor(isSponsor);
        RaiseNetworkEvent(eve);
    }
}
