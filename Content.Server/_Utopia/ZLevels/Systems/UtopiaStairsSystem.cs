using System.Numerics;
using Content.Server._Utopia.ZLevels.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.ZLevels.Systems;

public sealed partial class UtopiaStairsSystem : EntitySystem
{
    private static readonly TimeSpan TraverseCooldown = TimeSpan.FromSeconds(0.5);

    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    [SubscribeLocalEvent]
    private void OnStartCollide(Entity<UtopiaStairsComponent> stairs, ref StartCollideEvent args)
    {
        var entity = args.OtherEntity;
        if (HasComp<UtopiaStairsCooldownComponent>(entity))
            return;

        var xform = Transform(stairs);
        if (!xform.Anchored
        || xform.MapUid is not { } mapUid
        || !TryComp<CEZMapComponent>(mapUid, out var currentMap)
        || xform.GridUid is not { } gridUid
        || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var worldPos = _transform.GetWorldPosition(stairs);
        var forward = GetForward(_transform.GetWorldRotation(stairs).GetCardinalDir());
        var entityPos = _transform.GetWorldPosition(entity);
        var approachSide = Vector2.Dot(entityPos - worldPos, forward) >= 0f ? 1f : -1f;
        TryTraverse(entity, (mapUid, currentMap), grid, worldPos, forward, approachSide);
    }

    private void TryTraverse(EntityUid entity, Entity<CEZMapComponent?> currentMap, MapGridComponent currentGrid, Vector2 stairsPosition, Vector2 forward, float approachSide)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            var side = pass == 0 ? approachSide : -approachSide;
            var landingPosition = stairsPosition + forward * side * currentGrid.TileSize;

            if (_zLevels.TryMapOffset(currentMap, 1, out var mapAbove)
            && IsOpeningAt(mapAbove.Owner, stairsPosition)
            && !IsOpeningAt(mapAbove.Owner, landingPosition))
            {
                MoveToLevel(entity, currentMap, mapAbove.Owner, landingPosition, 1);
                return;
            }

            if (_zLevels.TryMapOffset(currentMap, -1, out var mapBelow)
            && IsOpeningAt(currentMap.Owner, landingPosition)
            && !IsOpeningAt(mapBelow.Owner, landingPosition))
            {
                MoveToLevel(entity, currentMap, mapBelow.Owner, landingPosition, -1);
                return;
            }
        }
    }

    private bool IsOpeningAt(EntityUid mapUid, Vector2 position)
    {
        if (!_map.TryFindGridAt(mapUid, position, out var gridUid, out var grid))
            return true;

        var tile = _map.WorldToTile(gridUid, grid, position);
        return CEZLevelOpeningCache.IsOpeningTile((gridUid, grid), tile, _map, _tileDefinitions);
    }

    private void MoveToLevel(EntityUid entity, Entity<CEZMapComponent?> currentMap, EntityUid targetMap, Vector2 landingPosition, int offset)
    {
        if (!_zLevels.TryMove(entity, offset, currentMap))
            return;

        EnsureComp<UtopiaStairsCooldownComponent>(entity);
        Timer.Spawn(TraverseCooldown, () =>
        {
            if (Exists(entity))
            {
                RemComp<UtopiaStairsCooldownComponent>(entity);
            }
        });

        var mapComponent = Comp<MapComponent>(targetMap);
        _transform.SetMapCoordinates(entity, new MapCoordinates(landingPosition, mapComponent.MapId));
    }

    private static Vector2 GetForward(Direction direction)
    {
        return direction switch
        {
            Direction.North => Vector2.UnitY,
            Direction.East => Vector2.UnitX,
            Direction.South => -Vector2.UnitY,
            Direction.West => -Vector2.UnitX,
            _ => Vector2.UnitY,
        };
    }
}
