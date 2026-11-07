using System.Numerics;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Moving.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Utopia.ZLevels.Moving.EntitySystems;

public sealed partial class UtopiaZLevelsMovingSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UtopiaZLevelsMovingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<UtopiaZLevelsMovingComponent, ComponentShutdown>(OnShutdown);

        SubscribeLocalEvent<UtopiaZLevelsMovingComponent, UtopiaZLevelsMovingActionEvent>(OnUpAction);
        SubscribeLocalEvent<UtopiaZLevelsMovingComponent, UtopiaZLevelsMovingDoAfterEvent>(OnUpDoAfter);

        SubscribeLocalEvent<UtopiaZLevelsMovingComponent, UtopiaZLevelsMovingDownActionEvent>(OnDownAction);
        SubscribeLocalEvent<UtopiaZLevelsMovingComponent, UtopiaZLevelsMovingDownDoAfterEvent>(OnDownDoAfter);
    }

    private void OnMapInit(Entity<UtopiaZLevelsMovingComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        _actions.AddAction(ent, ref ent.Comp.DownActionEntity, ent.Comp.DownAction);
    }

    private void OnShutdown(Entity<UtopiaZLevelsMovingComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
        _actions.RemoveAction(ent.Owner, ent.Comp.DownActionEntity);
    }

    #region Up

    private void OnUpAction(Entity<UtopiaZLevelsMovingComponent> ent, ref UtopiaZLevelsMovingActionEvent args)
    {
        if (args.Handled)
            return;

        var anchored = HasAnchoredNearby(ent);
        var doAfterArgs = new DoAfterArgs(EntityManager, ent, anchored ? ent.Comp.AnchoredDelay : ent.Comp.FloatingDelay,
            new UtopiaZLevelsMovingDoAfterEvent { Anchored = anchored }, ent)
        {
            BreakOnMove = anchored,
            BreakOnDamage = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        args.Handled = _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnUpDoAfter(Entity<UtopiaZLevelsMovingComponent> ent, ref UtopiaZLevelsMovingDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || !_zLevels.TryMapOffset(mapUid, 1, out var mapAbove))
        {
            _popup.PopupEntity(Loc.GetString("utopia-zlevels-moving-no-level"), ent, ent);
            return;
        }

        var worldPos = _transform.GetWorldPosition(xform);
        if (HasTile(mapAbove, worldPos))
        {
            _popup.PopupEntity(Loc.GetString("utopia-zlevels-moving-ceiling"), ent, ent);
            return;
        }

        MoveToMap(ent, mapAbove, worldPos);
        if (!args.Anchored)
            args.Repeat = true;
    }

    #endregion

    #region Down

    private void OnDownAction(Entity<UtopiaZLevelsMovingComponent> ent, ref UtopiaZLevelsMovingDownActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (GetDownFailure(ent) is { } failure)
        {
            _popup.PopupEntity(Loc.GetString(failure), ent, ent);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, ent, ent.Comp.DownDelay, new UtopiaZLevelsMovingDownDoAfterEvent(), ent)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnDownDoAfter(Entity<UtopiaZLevelsMovingComponent> ent, ref UtopiaZLevelsMovingDownDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (GetDownFailure(ent) is { } failure)
        {
            _popup.PopupEntity(Loc.GetString(failure), ent, ent);
            return;
        }

        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || !_zLevels.TryMapOffset(mapUid, -1, out var mapBelow))
            return;

        MoveToMap(ent, mapBelow, _transform.GetWorldPosition(xform));
    }
    private string? GetDownFailure(Entity<UtopiaZLevelsMovingComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || !_zLevels.TryMapOffset(mapUid, -1, out _))
            return "utopia-zlevels-moving-no-level-below";

        if (!HasAnchoredNearby(ent))
            return "utopia-zlevels-moving-no-support";

        if (HasTile(mapUid, _transform.GetWorldPosition(xform)))
            return "utopia-zlevels-moving-floor";

        return null;
    }

    #endregion

    #region Helpers

    private void MoveToMap(EntityUid uid, EntityUid targetMap, Vector2 worldPos)
    {
        var mapId = Comp<MapComponent>(targetMap).MapId;
        _transform.SetMapCoordinates(uid, new MapCoordinates(worldPos, mapId));
    }

    private bool HasAnchoredNearby(Entity<UtopiaZLevelsMovingComponent> ent)
    {
        if (HasComp<JetpackUserComponent>(ent))
            return true;

        var coords = _transform.GetMapCoordinates(ent);
        foreach (var other in _lookup.GetEntitiesInRange(coords, ent.Comp.StructureRange, LookupFlags.Static | LookupFlags.Sundries))
        {
            if (other == ent.Owner)
                continue;

            if (Transform(other).Anchored)
                return true;
        }

        return false;
    }
    private bool HasTile(EntityUid map, Vector2 worldPos)
    {
        if (!_map.TryFindGridAt(map, worldPos, out var gridUid, out var grid))
            return false;

        if (!_map.TryGetTileRef(gridUid, grid, worldPos, out var tileRef))
            return false;

        return !CEZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tileDefs);
    }

    #endregion
}
