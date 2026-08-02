using Content.Server.Popups;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Components;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Systems;

public sealed class ZLevelLadderSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelLadderComponent, ZLevelLadderMessage>(OnSelect);
        SubscribeLocalEvent<ZLevelLadderComponent, ZLevelLadderDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<ZLevelLadderComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    private void OnUiOpened(EntityUid uid, ZLevelLadderComponent comp, BoundUIOpenedEvent args)
    {
        var dirs = GetAvailableDirections(uid, comp);

        if (dirs.Count == 0)
        {
            _ui.CloseUi(uid, args.UiKey, args.Actor);
            return;
        }

        _ui.SetUiState(uid, args.UiKey, new ZLevelLadderBuiState(dirs));
    }

    private void OnSelect(EntityUid uid, ZLevelLadderComponent comp, ZLevelLadderMessage msg)
    {
        if (msg.Actor is not { Valid: true } user)
            return;

        if (!TryGetContext(uid, out var ctx))
            return;

        EntityUid? targetMap = null;

        switch (msg.Direction)
        {
            case ZMoveDirection.Up:
                if (comp.AllowUp)
                    targetMap = GetNeighborMap(ctx, 1);
                break;

            case ZMoveDirection.Down:
                if (comp.AllowDown)
                    targetMap = GetNeighborMap(ctx, -1);
                break;
        }

        if (targetMap == null)
        {
            return;
        }

        if (!IsValidDestination(uid, targetMap.Value, msg.Direction))
        {
            return;
        }

        var delay = HasComp<GhostComponent>(user) ? TimeSpan.Zero : comp.Delay;
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new ZLevelLadderDoAfterEvent(), uid, uid, uid)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
            BreakOnDamage = true,
            CancelDuplicate = true,
        };

        comp.Destination = targetMap;
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(EntityUid uid, ZLevelLadderComponent comp, ZLevelLadderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || comp.Deleted)
            return;

        if (comp.Destination != null)
        {
            Teleport(args.User, comp.Destination.Value);
        }
    }

    private List<ZMoveDirection> GetAvailableDirections(EntityUid uid, ZLevelLadderComponent comp)
    {
        var result = new List<ZMoveDirection>();

        if (!TryGetContext(uid, out var ctx))
            return result;

        if (comp.AllowUp && GetNeighborMap(ctx, 1) is { } up && IsValidDestination(uid, up, ZMoveDirection.Up))
            result.Add(ZMoveDirection.Up);

        if (comp.AllowDown && GetNeighborMap(ctx, -1) is { } down && IsValidDestination(uid, down, ZMoveDirection.Down))
            result.Add(ZMoveDirection.Down);

        return result;
    }

    private bool IsValidDestination(EntityUid source, EntityUid targetMap, ZMoveDirection direction)
    {
        var hasTile = HasTileAt(source, targetMap);
        var hasSourseTile = HasTileAt(source, source);

        return direction switch
        {
            ZMoveDirection.Down => !hasSourseTile,
            ZMoveDirection.Up => !hasTile,
            _ => false
        };
    }

    private bool HasTileAt(EntityUid source, EntityUid targetMap)
    {
        if (!TryGetAnchoredGrid(source, out var xform, out var gridUid, out var grid))
            return false;

        var worldBox = GetTileBox(gridUid, grid, xform);

        if (!TryComp(targetMap, out TransformComponent? mapXform))
            return false;

        var targetCoords = new MapCoordinates(worldBox.Center, mapXform.MapID);

        if (!_mapManager.TryFindGridAt(targetCoords, out var targetGridUid, out var targetGridComp))
            return false;

        var invMatrix = _transform.GetInvWorldMatrix(targetGridUid);
        var localPos = Vector2.Transform(worldBox.Center, invMatrix);
        var targetEntityCoords = new EntityCoordinates(targetGridUid, localPos);

        var tile = _map.GetTileRef(targetGridUid, targetGridComp, targetEntityCoords);

        return !tile.Tile.IsEmpty;
    }

    private void Teleport(EntityUid user, EntityUid targetMap)
    {
        var pos = _transform.GetWorldPosition(user);
        _transform.SetCoordinates(user, new EntityCoordinates(targetMap, pos));
    }

    #region Helpers

    private bool TryGetAnchoredGrid(EntityUid uid, out TransformComponent xform, out EntityUid gridUid, out MapGridComponent grid)
    {
        xform = default!;
        gridUid = default;
        grid = default!;

        if (!TryComp(uid, out TransformComponent? comp))
            return false;

        xform = comp;

        if (!xform.Anchored)
            return false;

        if (xform.GridUid is not { } g)
            return false;

        if (!TryComp(g, out MapGridComponent? mapGrid))
            return false;

        grid = mapGrid;
        gridUid = g;
        return true;
    }

    private Box2 GetTileBox(EntityUid gridUid, MapGridComponent grid, TransformComponent xform)
    {
        var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        var tileSize = grid.TileSize;
        var localCenter = new Vector2(tile.X + 0.5f, tile.Y + 0.5f) * tileSize;

        var worldMatrix = _transform.GetWorldMatrix(gridUid);
        var worldCenter = Vector2.Transform(localCenter, worldMatrix);

        var half = new Vector2(tileSize / 2f, tileSize / 2f);
        return new Box2(worldCenter - half, worldCenter + half);
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

        if (!_zLevels.TryGetZNetwork(mapUid, out var net) || net == null)
            return false;

        ctx = new ZLevelContext(xform, mapUid, zMap, net.Value.Owner);
        return true;
    }

    private EntityUid? GetNeighborMap(ZLevelContext ctx, int offset)
    {
        var mapEntity = new Entity<CEZLevelMapComponent?>(ctx.MapUid, ctx.ZMap);

        if (!_zLevels.TryMapOffset(mapEntity, offset, out var target) || target == null)
            return null;

        return target.Value.Owner;
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
