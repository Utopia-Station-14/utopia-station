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

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelTransmitterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelTransmitterComponent, MoveEvent>(OnMove);
    }

    private void OnStartup(EntityUid uid, ZLevelTransmitterComponent comp, ComponentStartup args)
    {
        Refresh(uid, comp);
    }

    private void OnMove(EntityUid uid, ZLevelTransmitterComponent comp, ref MoveEvent args)
    {
        Refresh(uid, comp);
    }

    private void Refresh(EntityUid uid, ZLevelTransmitterComponent transmitter)
    {
        if (!TryGetContext(uid, transmitter, out var context))
            return;

        UpdateBaseLink(uid, transmitter, context);
        UpdateAboveLink(uid, transmitter, context);
        UpdateBelowLink(uid, transmitter, context);

        if (HasComp<ZPipeComponent>(uid))
        {
            var zPipeSystem = EntityManager.System<ZPipeSystem>();
            zPipeSystem.TryLink(uid);
        }
    }

    private bool TryGetContext(
        EntityUid uid,
        ZLevelTransmitterComponent transmitter,
        out ZLevelContext context)
    {
        context = default;

        if (!TryComp(uid, out TransformComponent? xform))
            return false;

        if (xform.MapUid is not { } mapUid)
            return false;

        if (!TryComp(mapUid, out CEZLevelMapComponent? zMap))
            return false;

        if (!_zLevels.TryGetZNetwork(mapUid, out var networkEnt) ||
            networkEnt is not { } network)
            return false;

        context = new ZLevelContext(
            xform,
            mapUid,
            zMap,
            network.Owner
        );

        return true;
    }

    private void UpdateBaseLink(
        EntityUid uid,
        ZLevelTransmitterComponent transmitter,
        ZLevelContext context)
    {
        var link = EnsureComp<ZLevelEntityLinkComponent>(uid);

        link.ZNetwork = context.ZNetwork;
        link.Depth = context.ZMap.Depth;
        link.MapEntity = context.MapUid;
        link.GridEntity = transmitter.UseGrid ? context.Transform.GridUid : null;
    }

    private void UpdateAboveLink(
        EntityUid uid,
        ZLevelTransmitterComponent transmitter,
        ZLevelContext context)
    {
        var link = EnsureComp<ZLevelEntityLinkComponent>(uid);

        if (!transmitter.AllowUp)
        {
            link.AboveMap = null;
            return;
        }

        var mapEntity = new Entity<CEZLevelMapComponent?>(context.MapUid, context.ZMap);

        if (_zLevels.TryMapUp(mapEntity, out var above) &&
            above is { } aboveMap)
        {
            link.AboveMap = aboveMap.Owner;
        }
        else
        {
            link.AboveMap = null;
        }
    }

    private void UpdateBelowLink(
        EntityUid uid,
        ZLevelTransmitterComponent transmitter,
        ZLevelContext context)
    {
        var link = EnsureComp<ZLevelEntityLinkComponent>(uid);

        if (!transmitter.AllowDown)
        {
            link.BelowMap = null;
            return;
        }

        var mapEntity = new Entity<CEZLevelMapComponent?>(context.MapUid, context.ZMap);

        if (_zLevels.TryMapDown(mapEntity, out var below) &&
            below is { } belowMap)
        {
            link.BelowMap = belowMap.Owner;
        }
        else
        {
            link.BelowMap = null;
        }
    }
    
    private readonly struct ZLevelContext
    {
        public readonly TransformComponent Transform;
        public readonly EntityUid MapUid;
        public readonly CEZLevelMapComponent ZMap;
        public readonly EntityUid ZNetwork;

        public ZLevelContext(
            TransformComponent transform,
            EntityUid mapUid,
            CEZLevelMapComponent zMap,
            EntityUid zNetwork)
        {
            Transform = transform;
            MapUid = mapUid;
            ZMap = zMap;
            ZNetwork = zNetwork;
        }
    }
}
