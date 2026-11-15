/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Server._Utopia.ZLevels.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels;
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
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ZLevelsExtensions _zLevel = default!;

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
        var aSide = Vector2.Dot(entityPos - worldPos, forward) >= 0f ? 1f : -1f;
        TryTraverse(entity, (mapUid, currentMap), grid, worldPos, forward, aSide);
    }

    private void TryTraverse(EntityUid entity, Entity<CEZMapComponent?> currentMap, MapGridComponent currentGrid, Vector2 stairsPos, Vector2 forward, float aSide)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            var side = pass == 0 ? aSide : -aSide;
            var landingPos = stairsPos + forward * side * currentGrid.TileSize;

            if (_zLevels.TryMapOffset(currentMap, 1, out var mapAbove)
            && _zLevel.IsOpeningAt(mapAbove.Owner, stairsPos)
            && !_zLevel.IsOpeningAt(mapAbove.Owner, landingPos))
            {
                MoveToLevel(entity, currentMap, mapAbove.Owner, landingPos, 1);
                return;
            }

            if (_zLevels.TryMapOffset(currentMap, -1, out var mapBelow)
            && _zLevel.IsOpeningAt(currentMap.Owner, landingPos)
            && !_zLevel.IsOpeningAt(mapBelow.Owner, landingPos))
            {
                MoveToLevel(entity, currentMap, mapBelow.Owner, landingPos, -1);
                return;
            }
        }
    }

    private void MoveToLevel(EntityUid entity, Entity<CEZMapComponent?> currentMap, EntityUid targetMap, Vector2 landingPos, int offset)
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

        if (!TryComp<MapComponent>(targetMap, out var mapComp) || mapComp == null)
            return;

        _transform.SetMapCoordinates(entity, new MapCoordinates(landingPos, mapComp.MapId));
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
