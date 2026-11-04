using System.Numerics;
using Content.Server.Radiation.Components;
using Content.Server.Radiation.Systems;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using RadiationRay = Content.Shared.Radiation.Systems.RadiationRay;

namespace Content.Server._Utopia.Radiation.Systems;

public sealed partial class ZLevelRadiationSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;
    [Dependency] private RadiationSystem _radiation = default!;

    [Dependency] private EntityQuery<RadiationGridResistanceComponent> _resistanceQuery = default!;

    private List<Entity<MapGridComponent>> _grids = new();

    // Beta
    public const int MaxZLevels = 3;
    public const float LevelHeight = 3f;
    public const float FloorAbsorption = 1.5f;

    // Gamma
    public const int GammaMaxZLevels = 5;
    public const float GammaLevelHeight = 3f;
    public const float GammaFloorResistance = 0.02f;

    public bool TryGetLevelDiff(EntityUid? srcMap, EntityUid? dstMap, out int dz)
    {
        dz = 0;
        if (srcMap is not { } a || dstMap is not { } b)
            return false;

        return a == b || _zLevels.TryGetZLevelOffset(a, b, out dz);
    }

    public bool IsFloorOpen(EntityUid floorMap, Vector2 pos)
    {
        if (!_maps.TryFindGridAt(floorMap, pos, out var gridUid, out var grid))
            return true;

        return CEZLevelOpeningCache.IsOpeningTile(gridUid, grid, pos, _maps, _tileDef);
    }

    #region Beta
    public void CastBeta(int dz, EntityUid srcMapUid, MapId srcMapId, Vector2 srcWorld, Vector2 destWorld, ref RadiationRay ray, bool saveVisitedTiles)
    {
        var dir = destWorld - srcWorld;
        var steps = Math.Abs(dz);
        var sign = Math.Sign(dz);
        var curMapUid = srcMapUid;
        var curMapId = srcMapId;

        for (var i = 0; i <= steps; i++)
        {
            var a = srcWorld + dir * (i / (float) (steps + 1));
            var b = i == steps
                ? destWorld
                : srcWorld + dir * ((i + 1) / (float) (steps + 1));

            CastOnMap(curMapId, a, b, ref ray, saveVisitedTiles);
            if (ray.Rads <= 0)
                return;

            if (i == steps)
                return;

            if (!_zLevels.TryMapOffset(curMapUid, sign, out var next, out var nextMapComp))
            {
                ray.Rads = 0;
                return;
            }

            var floorMap = sign > 0 ? next.Owner : curMapUid;
            if (!IsFloorOpen(floorMap, b))
            {
                ray.Rads *= MathF.Exp(-RadiationSystem.BetaAbsorption * FloorAbsorption);
                if (ray.Rads <= _radiation.MinIntensity)
                {
                    ray.Rads = 0;
                    return;
                }
            }

            curMapUid = next.Owner;
            curMapId = nextMapComp.MapId;
        }
    }

    private void CastOnMap(MapId map, Vector2 a, Vector2 b, ref RadiationRay ray, bool saveVisitedTiles)
    {
        _grids.Clear();
        _maps.FindGridsIntersecting(map, Box2.FromTwoPoints(a, b), ref _grids, true);

        foreach (var grid in _grids)
        {
            GridcastSegment((grid.Owner, grid.Comp, Transform(grid)), a, b, ref ray, saveVisitedTiles);
            if (ray.Rads <= 0)
                break;
        }

        _grids.Clear();
    }

    private void GridcastSegment(Entity<MapGridComponent, TransformComponent> grid, Vector2 a, Vector2 b, ref RadiationRay ray, bool saveVisitedTiles)
    {
        if (!_resistanceQuery.TryGetComponent(grid.Owner, out var resistance))
            return;

        var srcLocal = Vector2.Transform(a, grid.Comp2.InvLocalMatrix) / grid.Comp1.TileSize;
        var dstLocal = Vector2.Transform(b, grid.Comp2.InvLocalMatrix) / grid.Comp1.TileSize;

        List<(Vector2i, float)>? blockers = saveVisitedTiles ? new() : null;

        foreach (var (point, dist) in RadiationSystem.AdvancedGridRaycast(srcLocal, dstLocal))
        {
            if (!resistance.ResistancePerTile.TryGetValue(point, out var res))
                continue;

            ray.Rads *= MathF.Exp(-RadiationSystem.BetaAbsorption * res * dist);
            blockers?.Add((point, ray.Rads));

            if (ray.Rads <= _radiation.MinIntensity)
            {
                ray.Rads = 0;
                break;
            }
        }

        if (blockers is { Count: > 0 })
        {
            ray.Blockers ??= new();
            ray.Blockers[GetNetEntity(grid.Owner)] = blockers;
        }
    }

    #endregion

    #region Gamma
    public float CastGamma(int dz, EntityUid srcMapUid, MapId srcMapId, Vector2 srcWorld, Vector2 destWorld, float rads)
    {
        var dir = destWorld - srcWorld;
        var scatterFloor = rads * GammaRadiationSystem.GammaScatterFraction;
        var steps = Math.Abs(dz);
        var sign = Math.Sign(dz);
        var curMapUid = srcMapUid;
        var curMapId = srcMapId;

        for (var i = 0; i <= steps; i++)
        {
            var a = srcWorld + dir * (i / (float) (steps + 1));
            var b = i == steps
                ? destWorld
                : srcWorld + dir * ((i + 1) / (float) (steps + 1));

            rads = GammaCastOnMap(curMapId, a, b, rads, scatterFloor);
            if (rads <= 0)
                return 0f;

            if (i == steps)
                break;

            if (!_zLevels.TryMapOffset(curMapUid, sign, out var next, out var nextMapComp))
                return 0f;

            var floorMap = sign > 0 ? next.Owner : curMapUid;
            if (!IsFloorOpen(floorMap, b))
            {
                rads *= MathF.Exp(-GammaRadiationSystem.GammaPenetration * GammaFloorResistance);
                if (rads < scatterFloor)
                    rads = scatterFloor;
                if (rads <= _radiation.MinIntensity)
                    return 0f;
            }

            curMapUid = next.Owner;
            curMapId = nextMapComp.MapId;
        }

        return rads;
    }

    private float GammaCastOnMap(MapId map, Vector2 a, Vector2 b, float rads, float scatterFloor)
    {
        _grids.Clear();
        _maps.FindGridsIntersecting(map, Box2.FromTwoPoints(a, b), ref _grids, true);

        foreach (var grid in _grids)
        {
            rads = GammaGridcastSegment(grid.Owner, grid.Comp, rads, scatterFloor, a, b);
            if (rads <= 0)
                break;
        }

        _grids.Clear();
        return rads;
    }

    private float GammaGridcastSegment(EntityUid gridUid, MapGridComponent grid, float rads,
        float scatterFloor, Vector2 a, Vector2 b)
    {
        if (!_resistanceQuery.TryComp(gridUid, out var resistance))
            return rads;

        var map = resistance.GammaResistancePerTile;
        if (map.Count == 0)
            return rads;

        var inv = Transform(gridUid).InvLocalMatrix;
        var srcGrid = Vector2.Transform(a, inv) / grid.TileSize;
        var dstGrid = Vector2.Transform(b, inv) / grid.TileSize;

        foreach (var (tile, distInCell) in RadiationSystem.AdvancedGridRaycast(srcGrid, dstGrid))
        {
            if (!map.TryGetValue(tile, out var res))
                continue;

            rads *= MathF.Exp(-GammaRadiationSystem.GammaPenetration * res * distInCell);
            if (rads < scatterFloor)
                rads = scatterFloor;

            if (rads <= _radiation.MinIntensity)
                return 0f;
        }

        return rads;
    }

    #endregion
}
