using Content.Server._Utopia.ZLevels.Pipes.Systems;
using Content.Server._Utopia.ZLevels.Nodes;
using Content.Server._Utopia.ZLevels.Power;
using Content.Shared._Utopia.ZLevels.Cables.Components;
using Content.Server.Disposal.Tube;
using Content.Server._Utopia.ZLevels.Disposal.Components;
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
    [Dependency] private readonly ZCableSystem _zCables = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelTransmitterComponent, ComponentStartup>(OnRefresh);
        SubscribeLocalEvent<ZLevelTransmitterComponent, MoveEvent>(OnMove);
    }

    #region Base
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
            RebuildPipeLinks(uid, link, transmitter);

        if (HasComp<ZCableComponent>(uid))
            RebuildCableLinks(uid, link, transmitter);
    }
    #endregion Base

    #region AtmosPipe
    private void RebuildPipeLinks(EntityUid uid, ZLevelEntityLinkComponent link, ZLevelTransmitterComponent transmitter)
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

        var worldBox = GetTileWorldBox(gridUid, grid, xform, transmitter);

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
    #endregion AtmosPipe

    #region Cables
    private void RebuildCableLinks(EntityUid uid, ZLevelEntityLinkComponent link, ZLevelTransmitterComponent transmitter)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored)
            return;

        if (!TryComp(uid, out NodeContainerComponent? container) || container.Nodes.Count == 0)
            return;

        if (xform.GridUid is not { } gridUid)
            return;

        if (!TryComp(gridUid, out MapGridComponent? grid))
            return;

        _zCables.ClearAll(container);

        var worldBox = GetTileWorldBox(gridUid, grid, xform, transmitter);

        foreach (var node in container.Nodes.Values)
        {
            if (node is not ZCableNode zNode)
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

            TryFindCableMatches(uid, zNode, worldBox, mapUid, requiredDir);
        }
    }
    private void TryFindCableMatches(
        EntityUid source,
        ZCableNode self,
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
                if (node is not ZCableNode other)
                    continue;

                if (other.ZDirection != requiredDir)
                    continue;

                _zCables.AddZConnection(self, other);
            }
        }
    }
    #endregion
    #region Disposal
    public EntityUid? TryFindZDisposalTarget(
        EntityUid source,
        EntityUid targetMap,
        ZNodeDirection dir)
    {
        if (!TryComp(source, out TransformComponent? srcXform))
            return null;

        if (srcXform.GridUid is not { } srcGridUid)
            return null;

        if (!TryComp(srcGridUid, out MapGridComponent? srcGrid))
            return null;

        var tile = srcGrid.TileIndicesFor(srcXform.Coordinates);
        if (!TryComp(targetMap, out TransformComponent? targetMapXform))
            return null;

        if (targetMapXform.GridUid is not { } targetGridUid)
            return null;

        if (!TryComp(targetGridUid, out MapGridComponent? targetGrid))
            return null;

        var tileSize = targetGrid.TileSize;
        var gridWorldPos = _transform.GetWorldPosition(targetGridUid);

        var tileOrigin = gridWorldPos +
            new Vector2(tile.X * tileSize, tile.Y * tileSize);

        var worldBox = new Box2(
            tileOrigin,
            tileOrigin + new Vector2(tileSize, tileSize)
        );

        var required = dir == ZNodeDirection.Up
            ? ZNodeDirection.Down
            : ZNodeDirection.Up;

        foreach (var ent in _lookup.GetEntitiesIntersecting(
                    targetMapXform.MapID,
                    worldBox,
                    LookupFlags.All))
        {
            if (!TryComp(ent, out ZDisposalPipeComponent? zPipe))
                continue;

            if (zPipe.ZDirection != required)
                continue;

            if (!TryComp(ent, out TransformComponent? xform) || !xform.Anchored)
                continue;

            if (!TryComp(ent, out DisposalTubeComponent? tube))
                continue;

            return ent;
        }

        return null;
    }
    #endregion

    #region General
    private Box2 GetTileWorldBox(
        EntityUid gridUid,
        MapGridComponent grid,
        TransformComponent xform,
        ZLevelTransmitterComponent transmitter)
    {
        var tile = grid.TileIndicesFor(xform.Coordinates);
        var tileSize = grid.TileSize;

        var tileOrigin =
            _transform.GetWorldPosition(gridUid) +
            new Vector2(tile.X * tileSize, tile.Y * tileSize);

        var range = transmitter.Range;
        var center = tileOrigin + new Vector2(tileSize / 2f, tileSize / 2f);

        var half = new Vector2(range / 2f, range / 2f);
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
    #endregion
}