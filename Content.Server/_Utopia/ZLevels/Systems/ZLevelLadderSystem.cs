using Content.Server.Popups;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Utopia.ZLevels.Systems;

public sealed class ZLevelLadderSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelLadderComponent, ZLevelLadderMessage>(OnSelect);
        SubscribeLocalEvent<ZLevelLadderComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    private void OnUiOpened(EntityUid uid, ZLevelLadderComponent comp, BoundUIOpenedEvent args)
    {
        var dirs = GetAvailableDirections(uid, comp);

        if (dirs.Count == 0)
        {
            _popup.PopupEntity("No way", uid, args.Actor);
            _ui.CloseUi(uid, args.UiKey, args.Actor);
            return;
        }

        comp.Directions = dirs;
        Dirty(uid, comp);
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
                    targetMap = TryGetNeighbor(ctx, 1);
                break;

            case ZMoveDirection.Down:
                if (comp.AllowDown)
                    targetMap = TryGetNeighbor(ctx, -1);
                break;
        }

        if (targetMap == null)
        {
            _popup.PopupEntity("No way", uid, user);
            return;
        }

        if (!HasValidTile(uid, targetMap.Value))
        {
            _popup.PopupEntity("Blocked", uid, user);
            return;
        }

        Teleport(user, targetMap.Value);
    }

    private List<ZMoveDirection> GetAvailableDirections(EntityUid uid, ZLevelLadderComponent comp)
    {
        var result = new List<ZMoveDirection>();

        if (!TryGetContext(uid, out var ctx))
            return result;

        if (comp.AllowUp && TryGetNeighbor(ctx, 1) is { } up && HasValidTile(uid, up))
            result.Add(ZMoveDirection.Up);

        if (comp.AllowDown && TryGetNeighbor(ctx, -1) is { } down && HasValidTile(uid, down))
            result.Add(ZMoveDirection.Down);

        return result;
    }

    private bool HasValidTile(EntityUid source, EntityUid targetMap)
    {
        var coords = Transform(source).Coordinates;

        if (coords.EntityId == EntityUid.Invalid)
            return false;

        if (!TryComp<MapGridComponent>(coords.EntityId, out var grid))
            return false;

        if (Transform(coords.EntityId).MapUid != targetMap)
            return false;

        var tile = _map.GetTileRef(coords.EntityId, grid, coords);

        return tile.Tile.IsEmpty;
    }

    private void Teleport(EntityUid user, EntityUid targetMap)
    {
        var pos = _xform.GetMapCoordinates(user).Position;
        _xform.SetCoordinates(user, new EntityCoordinates(targetMap, pos));
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

        ctx = new ZLevelContext(mapUid, zMap, net.Value.Owner);
        return true;
    }

    private EntityUid? TryGetNeighbor(ZLevelContext ctx, int offset)
    {
        var mapEntity = new Entity<CEZLevelMapComponent?>(ctx.MapUid, ctx.ZMap);

        return _zLevels.TryMapOffset(mapEntity, offset, out var target) && target != null
            ? target.Value.Owner
            : null;
    }

    private readonly struct ZLevelContext
    {
        public readonly EntityUid MapUid;
        public readonly CEZLevelMapComponent ZMap;
        public readonly EntityUid Network;

        public ZLevelContext(EntityUid mapUid, CEZLevelMapComponent zMap, EntityUid network)
        {
            MapUid = mapUid;
            ZMap = zMap;
            Network = network;
        }
    }
}
