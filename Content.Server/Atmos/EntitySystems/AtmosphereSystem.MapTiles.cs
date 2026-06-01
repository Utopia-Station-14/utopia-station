using System.Numerics;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private EntityQuery<MapAtmosphereSimulationComponent> _mapSimQuery;

    private readonly List<(EntityUid MapUid, MapAtmosphereSimulationComponent Sim)> _currentRunMapAtmosphere = new();
    private int _currentRunMapAtmosphereIndex;
    private bool _mapSimulationPaused;

    private void InitializeMapTiles()
    {
        _mapSimQuery = GetEntityQuery<MapAtmosphereSimulationComponent>();
    }

    private MapAtmosphereSimulationComponent EnsureMapSimulation(EntityUid mapUid)
    {
        return EnsureComp<MapAtmosphereSimulationComponent>(mapUid);
    }

    private static Vector2i WorldTileOffset(Vector2i worldTile, AtmosDirection direction)
    {
        return worldTile.Offset(direction);
    }

    private Vector2i GridTileToWorldTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        return _map.GridTileToWorldPos(gridUid, grid, indices).Floored();
    }

    private Vector2 WorldTileCenter(Vector2i worldTile)
    {
        return new Vector2(worldTile.X + 0.5f, worldTile.Y + 0.5f);
    }

    /// <summary>
    /// Resolves an atmos cell at a world tile, preferring a grid tile when one exists there.
    /// </summary>
    private bool TryResolveAtmosTileAtWorld(
        EntityUid mapUid,
        Vector2i worldTile,
        bool create,
        out TileAtmosphere? tile,
        out EntityUid ownerUid,
        out bool onMapSim)
    {
        tile = null;
        ownerUid = EntityUid.Invalid;
        onMapSim = false;

        var worldCenter = WorldTileCenter(worldTile);

        if (_mapManager.TryFindGridAt(mapUid, worldCenter, out var gridUid, out var gridComp)
            && TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos)
            && TryComp<TransformComponent>(gridUid, out var gridXform)
            && gridXform.MapUid == mapUid)
        {
            var gridIndices = _map.WorldToTile(gridUid, gridComp, worldCenter);
            ownerUid = gridUid;
            onMapSim = false;

            if (!create)
            {
                return gridAtmos.Tiles.TryGetValue(gridIndices, out tile);
            }

            tile = GetOrNewTile(gridUid, gridAtmos, gridIndices, out _, invalidateNew: true);
            return true;
        }

        if (!TryComp(mapUid, out MapAtmosphereComponent? _))
            return false;

        var sim = EnsureMapSimulation(mapUid);
        ownerUid = mapUid;
        onMapSim = true;

        if (!sim.Tiles.TryGetValue(worldTile, out tile))
        {
            if (!create)
                return false;

            EnsureMapAtmosTile(mapUid, worldTile);
            tile = sim.Tiles.GetValueOrDefault(worldTile);
            sim.InvalidatedCoords.Add(worldTile);
        }

        if (tile != null)
            EnsureMapTileHasAir(tile);

        return tile != null;
    }

    private bool IsAdjacentBlocked(
        TileAtmosphere source,
        AtmosDirection sourceDir,
        TileAtmosphere target,
        AtmosDirection targetOppositeDir)
    {
        if (!source.NoGridTile && source.AirtightData.BlockedDirections.IsFlagSet(sourceDir))
            return true;

        if (!target.NoGridTile && target.AirtightData.BlockedDirections.IsFlagSet(targetOppositeDir))
            return true;

        return false;
    }

    private static void LinkAdjacentTiles(
        TileAtmosphere source,
        TileAtmosphere target,
        int sourceIndex,
        int targetIndex,
        AtmosDirection sourceDir,
        AtmosDirection targetDir)
    {
        source.AdjacentBits |= sourceDir;
        target.AdjacentBits |= targetDir;
        source.AdjacentTiles[sourceIndex] = target;
        target.AdjacentTiles[targetIndex] = source;
    }

    private static void UnlinkAdjacentTiles(
        TileAtmosphere source,
        TileAtmosphere target,
        int sourceIndex,
        int targetIndex,
        AtmosDirection sourceDir,
        AtmosDirection targetDir)
    {
        source.AdjacentBits &= ~sourceDir;
        target.AdjacentBits &= ~targetDir;
        source.AdjacentTiles[sourceIndex] = null;
        target.AdjacentTiles[targetIndex] = null;
    }

    private void UpdateMapAdjacentTiles(
        EntityUid mapUid,
        MapAtmosphereSimulationComponent sim,
        TileAtmosphere tile,
        bool activate = false)
    {
        if (activate)
            AddMapActiveTile(sim, tile);

        tile.AdjacentBits = AtmosDirection.Invalid;
        var worldTile = tile.GridIndices;

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection)(1 << i);
            var adjacentWorld = WorldTileOffset(worldTile, direction);

            if (!TryResolveAtmosTileAtWorld(mapUid, adjacentWorld, create: true, out var adjacent, out var ownerUid, out var onMapSim)
                || adjacent == null)
            {
                continue;
            }

            if (activate)
            {
                if (onMapSim)
                    AddMapActiveTile(sim, adjacent);
                else if (TryComp<GridAtmosphereComponent>(ownerUid, out var gridAtmos))
                    AddActiveTile(gridAtmos, adjacent);
            }

            var oppositeIndex = i.ToOppositeIndex();
            var oppositeDirection = (AtmosDirection)(1 << oppositeIndex);

            if (IsAdjacentBlocked(tile, direction, adjacent, oppositeDirection))
            {
                UnlinkAdjacentTiles(tile, adjacent, i, oppositeIndex, direction, oppositeDirection);
            }
            else
            {
                LinkAdjacentTiles(tile, adjacent, i, oppositeIndex, direction, oppositeDirection);
            }

            DebugTools.Assert(!(tile.AdjacentBits.IsFlagSet(direction) ^
                                adjacent.AdjacentBits.IsFlagSet(oppositeDirection)));

            if (!adjacent.AdjacentBits.IsFlagSet(adjacent.MonstermosInfo.CurrentTransferDirection))
                adjacent.MonstermosInfo.CurrentTransferDirection = AtmosDirection.Invalid;
        }

        if (!tile.AdjacentBits.IsFlagSet(tile.MonstermosInfo.CurrentTransferDirection))
            tile.MonstermosInfo.CurrentTransferDirection = AtmosDirection.Invalid;
    }

    private void UpdateMapTileData(EntityUid mapUid, MapAtmosphereSimulationComponent sim, TileAtmosphere tile)
    {
        var spaceDef = (ContentTileDefinition)_tileDefinitionManager[ContentTileDefinition.SpaceID];
        tile.ThermalConductivity = spaceDef.ThermalConductivity;
        tile.HeatCapacity = spaceDef.HeatCapacity;
        tile.NoGridTile = true;
        tile.Space = false;
        tile.MapAtmosphere = false;
        tile.AirtightData = default;
    }

    private void SyncMapAtmosTile(EntityUid mapUid, Vector2i worldTile)
    {
        if (!TryComp(mapUid, out MapAtmosphereComponent? _))
            return;

        var sim = EnsureMapSimulation(mapUid);
        var tile = GetOrNewMapTile(mapUid, sim, worldTile, out _, invalidateNew: false);
        UpdateMapTileData(mapUid, sim, tile);
        UpdateMapAdjacentTiles(mapUid, sim, tile, activate: true);
        EnsureMapTileHasAir(tile);

        if (tile.Air != null)
            AddMapActiveTile(sim, tile);

        InvalidateMapVisuals(mapUid, worldTile);
    }

    private TileAtmosphere GetOrNewMapTile(
        EntityUid mapUid,
        MapAtmosphereSimulationComponent sim,
        Vector2i worldTile,
        out bool existing,
        bool invalidateNew = true)
    {
        var tile = sim.Tiles.GetOrNew(worldTile, out existing);
        if (existing)
            return tile;

        if (invalidateNew)
            sim.InvalidatedCoords.Add(worldTile);

        tile.GridIndex = mapUid;
        tile.GridIndices = worldTile;
        tile.NoGridTile = true;
        return tile;
    }

    [PublicAPI]
    public void EnsureMapAtmosTile(EntityUid mapUid, Vector2i worldTile)
    {
        if (!TryComp(mapUid, out MapAtmosphereComponent? _))
            return;

        var sim = EnsureMapSimulation(mapUid);
        if (sim.Tiles.TryGetValue(worldTile, out var existing))
        {
            EnsureMapTileHasAir(existing);
            return;
        }

        var tile = new TileAtmosphere(mapUid, worldTile, new GasMixture(Atmospherics.CellVolume)
        {
            Temperature = Atmospherics.T20C
        })
        {
            NoGridTile = true,
            MapAtmosphere = false,
            Space = false
        };

        sim.Tiles[worldTile] = tile;
    }

    private void EnsureMapTileHasAir(TileAtmosphere tile)
    {
        if (tile.Air is { Immutable: false })
            return;

        tile.Air = new GasMixture(Atmospherics.CellVolume)
        {
            Temperature = Atmospherics.T20C
        };
        tile.NoGridTile = true;
        tile.MapAtmosphere = false;
        tile.Space = false;
    }

    private void AddMapActiveTile(MapAtmosphereSimulationComponent sim, TileAtmosphere tile)
    {
        if (tile.Air == null || tile.Excited)
            return;

        tile.Excited = true;
        sim.ActiveTiles.Add(tile);
    }

    private void RemoveMapActiveTile(MapAtmosphereSimulationComponent sim, TileAtmosphere tile, bool disposeExcitedGroup = true)
    {
        DebugTools.Assert(tile.Excited == sim.ActiveTiles.Contains(tile));
        DebugTools.Assert(tile.Excited || tile.ExcitedGroup == null);

        if (!tile.Excited)
            return;

        tile.Excited = false;
        sim.ActiveTiles.Remove(tile);

        if (tile.ExcitedGroup == null)
            return;

        if (disposeExcitedGroup)
            MapExcitedGroupDispose(sim, tile.ExcitedGroup);
        else
            MapExcitedGroupRemoveTile(tile.ExcitedGroup, tile);
    }

    private GasMixture? GetMapTileMixture(EntityUid mapUid, Vector2i worldTile, bool excite = false)
    {
        if (!TryComp(mapUid, out MapAtmosphereComponent? mapAtmos))
            return GasMixture.SpaceGas;

        var sim = EnsureMapSimulation(mapUid);
        if (!sim.Tiles.TryGetValue(worldTile, out var tile))
        {
            if (!excite)
                return mapAtmos.Mixture;

            EnsureMapAtmosTile(mapUid, worldTile);
            SyncMapAtmosTile(mapUid, worldTile);
            tile = sim.Tiles.GetValueOrDefault(worldTile);
        }

        if (tile == null)
            return mapAtmos.Mixture;

        EnsureMapTileHasAir(tile);

        if (excite)
        {
            AddMapActiveTile(sim, tile);
            sim.InvalidatedCoords.Add(worldTile);
            InvalidateMapVisuals(mapUid, worldTile);
        }

        return tile.Air;
    }

    public void InvalidateMapVisuals(EntityUid mapUid, Vector2i worldTile)
    {
        if (!_mapSimQuery.TryGetComponent(mapUid, out var sim))
            return;

        sim.InvalidOverlayTiles.Add(worldTile);
    }

    private void InvalidateMapVisuals(EntityUid mapUid, TileAtmosphere tile)
    {
        InvalidateMapVisuals(mapUid, tile.GridIndices);
    }
}
