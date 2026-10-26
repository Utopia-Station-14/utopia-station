using System.Numerics;
using Content.Server.Spreader;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SpreaderSystem _spreader = default!;

    private const float ZTransferFraction = 0.5f;
    private const float ZMinTransferIntensity = 5f;

    internal bool HasZLevel(EntityUid mapUid, int offset)
        => _zLevels.TryMapOffset(mapUid, offset, out _);

    internal bool IsZHole(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
        => _spreader.IsHole(gridUid, grid, indices);

    internal Vector2 GetGridTileWorldPos(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var local = _map.GridTileToLocal(gridUid, grid, tile);
        return _transformSystem.ToMapCoordinates(local).Position;
    }

    private bool TryGetZTileAt(EntityUid mapUid, Vector2 worldPos, int offset,
        out EntityUid gridUid, out MapGridComponent grid, out Vector2i indices)
    {
        gridUid = default;
        grid = default!;
        indices = default;

        if (!_zLevels.TryMapOffset(mapUid, offset, out var targetMap))
            return false;

        if (!_map.TryFindGridAt(targetMap.Owner, worldPos, out var foundUid, out var foundGrid) || foundGrid == null)
            return false;

        gridUid = foundUid;
        grid = foundGrid;

        var local = Vector2.Transform(worldPos, Transform(gridUid).InvLocalMatrix);
        indices = _map.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, local));
        return true;
    }

    internal void AccumulateZLeak(Dictionary<int, ExplosionZLeak> leaks, EntityUid mapUid, Vector2 worldPos,
        float intensity, bool canUp, bool canDown, bool holeHere, bool canBreakCeiling = false)
    {
        if (intensity <= 0f)
            return;

        if (canDown && holeHere && TryGetZTileAt(mapUid, worldPos, -1, out _, out _, out _))
            AddZLeak(leaks, -1, worldPos, intensity);

        if (canUp && TryGetZTileAt(mapUid, worldPos, 1, out var upUid, out var upGrid, out var upIndices) &&
            (canBreakCeiling || _spreader.IsHole(upUid, upGrid, upIndices)))
        {
            AddZLeak(leaks, 1, worldPos, intensity);
        }
    }

    private static void AddZLeak(Dictionary<int, ExplosionZLeak> leaks, int offset, Vector2 worldPos, float intensity)
    {
        if (!leaks.TryGetValue(offset, out var leak))
        {
            leak = new ExplosionZLeak();
            leaks[offset] = leak;
        }

        leak.TotalIntensity += intensity;
        leak.WeightedPosition += worldPos * intensity;
    }

    internal void QueueZExplosion(
        MapId sourceMapId,
        int offset,
        ExplosionZLeak leak,
        string typeId,
        float slope,
        float maxTileIntensity,
        EntityUid? cause,
        float tileBreakScale,
        int maxTileBreak,
        bool canCreateVacuum)
    {
        var totalIntensity = leak.TotalIntensity * ZTransferFraction;
        if (totalIntensity < ZMinTransferIntensity)
            return;

        if (!_map.MapExists(sourceMapId))
            return;

        var sourceMap = _map.GetMap(sourceMapId);
        if (!_zLevels.TryMapOffset(sourceMap, offset, out var targetMap))
            return;

        var targetMapId = Comp<MapComponent>(targetMap.Owner).MapId;
        var position = leak.WeightedPosition / leak.TotalIntensity;

        QueueExplosion(new MapCoordinates(position, targetMapId),
            typeId,
            totalIntensity,
            slope,
            maxTileIntensity,
            cause,
            tileBreakScale,
            maxTileBreak,
            canCreateVacuum,
            zDirection: offset);
    }
}
public sealed class ExplosionZLeak
{
    public float TotalIntensity;
    public Vector2 WeightedPosition;
}
