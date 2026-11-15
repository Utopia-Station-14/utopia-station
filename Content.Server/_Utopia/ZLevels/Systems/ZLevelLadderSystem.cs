/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Popups;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Components;
using Content.Shared.DoAfter;
using Content.Shared.Ghost.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Systems;

public sealed partial class ZLevelLadderSystem : EntitySystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
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
            return;

        if (!IsValidDestination(uid, targetMap.Value, msg.Direction))
            return;

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
        return direction switch
        {
            ZMoveDirection.Down => !HasTileAt(source, source),
            ZMoveDirection.Up => !HasTileAt(targetMap, _transform.GetWorldPosition(source)),
            _ => false
        };
    }

    private bool HasTileAt(EntityUid source, EntityUid targetMap)
    {
        return HasTileAt(targetMap, _transform.GetWorldPosition(source));
    }

    private bool HasTileAt(EntityUid targetMap, Vector2 position)
    {
        if (!_map.TryFindGridAt(targetMap, position, out var targetGridUid, out var targetGridComp))
            return false;

        return _map.TryGetTileRef(targetGridUid, targetGridComp, position, out var tile)
            && !tile.Tile.IsEmpty;
    }

    private void Teleport(EntityUid user, EntityUid targetMap)
    {
        var pos = _transform.GetWorldPosition(user);
        _transform.SetCoordinates(user, new EntityCoordinates(targetMap, pos));
    }

    #region Helpers

    private bool TryGetContext(EntityUid uid, out ZLevelContext ctx)
    {
        ctx = default;

        if (!TryComp(uid, out TransformComponent? xform))
            return false;

        if (xform.MapUid is not { } mapUid)
            return false;

        if (!TryComp(mapUid, out CEZMapComponent? zMap))
            return false;

        if (!_zLevels.TryGetMapNetwork(mapUid, out var net))
            return false;

        ctx = new ZLevelContext(xform, mapUid, zMap, net.Owner);
        return true;
    }

    private EntityUid? GetNeighborMap(ZLevelContext ctx, int offset)
    {
        var mapEntity = new Entity<CEZMapComponent?>(ctx.MapUid, ctx.ZMap);

        if (!_zLevels.TryMapOffset(mapEntity, offset, out var target))
            return null;

        return target.Owner;
    }

    private readonly struct ZLevelContext
    {
        public readonly TransformComponent Transform;
        public readonly EntityUid MapUid;
        public readonly CEZMapComponent ZMap;
        public readonly EntityUid ZNetwork;

        public ZLevelContext(
            TransformComponent transform,
            EntityUid mapUid,
            CEZMapComponent zMap,
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
