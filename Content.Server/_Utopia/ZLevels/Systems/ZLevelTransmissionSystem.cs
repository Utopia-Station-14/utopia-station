using Content.Server._Utopia.ZLevels.Pipes.Systems;
using Content.Server._Utopia.ZLevels.Nodes;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.NodeContainer;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Pipes.Components;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Transmission.Systems;

public sealed class ZLevelTransmissionSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly ZPipeSystem _zPipes = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelTransmitterComponent, ComponentStartup>(OnRefresh);
        SubscribeLocalEvent<ZLevelTransmitterComponent, MoveEvent>(OnMove);
    }

    private void OnRefresh(EntityUid uid, ZLevelTransmitterComponent comp, ComponentStartup args)
        => Refresh(uid, comp);

    private void OnMove(EntityUid uid, ZLevelTransmitterComponent comp, ref MoveEvent args)
        => Refresh(uid, comp);

    private void Refresh(EntityUid uid, ZLevelTransmitterComponent transmitter)
    {
        if (!TryGetContext(uid, out var ctx))
            return;

        var link = EnsureComp<ZLevelEntityLinkComponent>(uid);

        link.ZNetwork = ctx.ZNetwork;
        link.Depth = ctx.ZMap.Depth;
        link.MapEntity = ctx.MapUid;
        link.GridEntity = transmitter.UseGrid ? ctx.Transform.GridUid : null;

        link.AboveMap = transmitter.AllowUp
            ? GetNeighborMap(ctx, +1)
            : null;

        link.BelowMap = transmitter.AllowDown
            ? GetNeighborMap(ctx, -1)
            : null;

        if (HasComp<ZPipeComponent>(uid))
            RebuildPipeLinks(uid, link);
    }

    private void RebuildPipeLinks(EntityUid uid, ZLevelEntityLinkComponent link)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored)
            return;

        if (!TryComp(uid, out NodeContainerComponent? container) || container.Nodes.Count == 0)
            return;

        if (xform.GridUid is not { } gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        _zPipes.ClearAll(container);

        var worldBox = GetTileWorldBox(gridUid, grid, xform);

        foreach (var node in container.Nodes.Values)
        {
            if (node is not ZPipeNode zNode)
                continue;

            EntityUid? targetMap;
            ZNodeDirection requiredDir;

            switch (zNode.ZDirection)
            {
                case ZNodeDirection.Up:
                    targetMap = link.AboveMap;
                    requiredDir = ZNodeDirection.Down;
                    break;

                case ZNodeDirection.Down:
                    targetMap = link.BelowMap;
                    requiredDir = ZNodeDirection.Up;
                    break;

                default:
                    continue;
            }

            if (targetMap is not { } mapUid)
                continue;

            TryFindPipeMatches(uid, zNode, worldBox, mapUid, requiredDir);
        }
    }

    private void TryFindPipeMatches(
        EntityUid source,
        ZPipeNode self,
        Box2 worldBox,
        EntityUid targetMap,
        ZNodeDirection requiredDir)
    {
        if (!TryComp(targetMap, out TransformComponent? mapXform))
            return;

        var mapId = mapXform.MapID;

        foreach (var ent in _lookup.GetEntitiesIntersecting(
                    mapId,
                    worldBox,
                    LookupFlags.All))
        {
            if (ent == source)
                continue;

            if (!TryComp(ent, out TransformComponent? xform) || !xform.Anchored)
                continue;

            if (!TryComp(ent, out NodeContainerComponent? container))
                continue;

            foreach (var node in container.Nodes.Values)
            {
                if (node is not ZPipeNode other)
                    continue;

                if (other.ZDirection != requiredDir)
                    continue;

                _zPipes.AddZConnection(self, other);
            }
        }
    }

    private Box2 GetTileWorldBox(
        EntityUid gridUid,
        MapGridComponent grid,
        TransformComponent xform)
    {
        var tile = grid.TileIndicesFor(xform.Coordinates);
        var tileSize = grid.TileSize;

        var tileOrigin =
            _transform.GetWorldPosition(gridUid) +
            new Vector2(tile.X * tileSize, tile.Y * tileSize);

        var center = tileOrigin + new Vector2(tileSize / 2f, tileSize / 2f);
        const float epsilon = 0.01f; // вынести в компонент

        var half = new Vector2(epsilon / 2f, epsilon / 2f);
        return new Box2(center - half, center + half);
    }

    private bool TryGetContext(EntityUid uid, out ZLevelContext ctx)
    {
        ctx = default;

        if (!TryComp(uid, out TransformComponent? xform))
            return false;

        if (xform.MapUid is not { } mapUid)
            return false;

        if (!TryComp(mapUid, out CEZLevelMapComponent? zMap))
            return false;

        if (!_zLevels.TryGetZNetwork(mapUid, out var net) || net is null)
            return false;

        ctx = new ZLevelContext(
            xform,
            mapUid,
            zMap,
            net.Value.Owner);

        return true;
    }

    private EntityUid? GetNeighborMap(ZLevelContext ctx, int offset)
    {
        var mapEntity = new Entity<CEZLevelMapComponent?>(ctx.MapUid, ctx.ZMap);

        if (offset > 0)
        {
            if (!_zLevels.TryMapUp(mapEntity, out var above) || above is null)
                return null;

            return above.Value.Owner;
        }

        if (!_zLevels.TryMapDown(mapEntity, out var below) || below is null)
            return null;

        return below.Value.Owner;
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
