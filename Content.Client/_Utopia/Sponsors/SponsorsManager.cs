using System.Diagnostics.CodeAnalysis;
using Content.Shared._Utopia.Sponsors;
using Robust.Shared.Network;

namespace Content.Client._Utopia.Sponsors;

public sealed partial class SponsorsManager
{
    [Dependency] private IClientNetManager _netMgr = default!;

    private SponsorInfo? _info;

    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSponsorInfo>(msg => _info = msg.Info);
    }

    public bool TryGetInfo([NotNullWhen(true)] out SponsorInfo? sponsor)
    {
        sponsor = _info;
        return _info != null;
    }
}
