using Content.Server._Utopia.ZLevels.Pipes.Systems;
using Content.Shared._Utopia.ZLevels.Pipes.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._Utopia.ZLevels.Transmission.Systems;

public sealed class ZLevelTransmissionSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly ZPipeSystem _zPipes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelTransmitterComponent, ComponentStartup>(OnTransmitterStartup);
        SubscribeLocalEvent<ZLevelTransmitterComponent, MoveEvent>(OnTransmitterMove);
    }

    private void OnTransmitterStartup(EntityUid uid, ZLevelTransmitterComponent comp, ComponentStartup args)
    {
        Refresh(uid, comp);
    }

    private void OnTransmitterMove(EntityUid uid, ZLevelTransmitterComponent comp, ref MoveEvent args)
    {
        Refresh(uid, comp);
    }

    private void Refresh(EntityUid uid, ZLevelTransmitterComponent transmitter)
    {
        if (!TryGetContext(uid, out var ctx))
            return;

        var link = EnsureComp<ZLevelEntityLinkComponent>(uid);
        link.ZNetwork = ctx.ZNetwork;
        link.Depth = ctx.ZMap.Depth;
        link.MapEntity = ctx.MapUid;
        link.GridEntity = transmitter.UseGrid ? ctx.Transform.GridUid : null;

        link.AboveMap = transmitter.AllowUp ? GetNeighborMap(ctx, 1) : null;
        link.BelowMap = transmitter.AllowDown ? GetNeighborMap(ctx, -1) : null;

        if (HasComp<ZPipeComponent>(uid))
            _zPipes.TryLink(uid);
    }

    private bool TryGetContext(EntityUid uid, out ZLevelContext ctx)
    {
        ctx = default;

        if (!TryComp(uid, out TransformComponent? xform)
            || xform.MapUid is not { } mapUid
            || !TryComp(mapUid, out CEZLevelMapComponent? zMap)
            || !_zLevels.TryGetZNetwork(mapUid, out var network) || network is not { } net)
        {
            return false;
        }

        ctx = new ZLevelContext(xform, mapUid, zMap, net.Owner);
        return true;
    }

    private EntityUid? GetNeighborMap(ZLevelContext ctx, int offset)
    {
        var mapEntity = new Entity<CEZLevelMapComponent?>(ctx.MapUid, ctx.ZMap);
        if (offset > 0)
            return _zLevels.TryMapUp(mapEntity, out var above) && above is { } a ? a.Owner : null;
        return _zLevels.TryMapDown(mapEntity, out var below) && below is { } b ? b.Owner : null;
    }

    private readonly struct ZLevelContext
    {
        public readonly TransformComponent Transform;
        public readonly EntityUid MapUid;
        public readonly CEZLevelMapComponent ZMap;
        public readonly EntityUid ZNetwork;

        public ZLevelContext(TransformComponent transform, EntityUid mapUid, CEZLevelMapComponent zMap, EntityUid zNetwork)
        {
            Transform = transform;
            MapUid = mapUid;
            ZMap = zMap;
            ZNetwork = zNetwork;
        }
    }
}
