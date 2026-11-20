using Content.Shared._Utopia.ZLevels;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;

namespace Content.Shared.DeviceNetwork.Systems;

public sealed partial class WirelessNetworkSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private ZLevelsExtensions _zLevel = default!; // Utopia-Tweak : ZLevels

    [Dependency] private EntityQuery<WirelessNetworkComponent> _wirelessQuery;

    [SubscribeLocalEvent]
    private void OnBeforePacketSent(Entity<WirelessNetworkComponent> ent, ref BeforePacketSentEvent args)
    {
        var ownPosition = args.SenderPosition;
        var xform = Transform(ent);

        // not a wireless to wireless connection, just let it happen
        if (!_wirelessQuery.TryComp(args.Sender, out var sendingComponent))
            return;

        // Utopia-Tweak : ZLevels
        var sameMap = xform.MapID == args.SenderTransform.MapID;
        if (!sameMap && !_zLevel.IsSameZNetwork(xform.MapUid, args.SenderTransform.MapUid))
        {
            args.Cancelled = true;
            return;
        }

        if (sendingComponent.Range != null &&
        (ownPosition - _transformSystem.GetWorldPosition(xform)).Length() > sendingComponent.Range)
        {
            args.Cancelled = true;
        }
        // Utopia-Tweak : ZLevels
    }
}
