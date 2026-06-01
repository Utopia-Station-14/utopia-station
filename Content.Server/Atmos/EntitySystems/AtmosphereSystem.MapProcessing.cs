using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private void UpdateMapProcessing(float frameTime)
    {
        _simulationStopwatch.Restart();

        if (!_mapSimulationPaused)
        {
            _currentRunMapAtmosphereIndex = 0;
            _currentRunMapAtmosphere.Clear();

            var query = EntityQueryEnumerator<MapAtmosphereSimulationComponent, MapAtmosphereComponent>();
            while (query.MoveNext(out var uid, out var sim, out _))
            {
                _currentRunMapAtmosphere.Add((uid, sim));
            }
        }

        _mapSimulationPaused = true;

        for (; _currentRunMapAtmosphereIndex < _currentRunMapAtmosphere.Count; _currentRunMapAtmosphereIndex++)
        {
            var (mapUid, sim) = _currentRunMapAtmosphere[_currentRunMapAtmosphereIndex];

            if (sim.LifeStage >= ComponentLifeStage.Stopping || Paused(mapUid) || !sim.Simulated)
                continue;

            var completionState = ProcessMapAtmosphere(mapUid, sim, frameTime);

            switch (completionState)
            {
                case AtmosphereProcessingCompletionState.Return:
                    return;
                case AtmosphereProcessingCompletionState.Continue:
                    continue;
                case AtmosphereProcessingCompletionState.Finished:
                    break;
            }
        }

        _mapSimulationPaused = false;
    }

    private AtmosphereProcessingCompletionState ProcessMapAtmosphere(
        EntityUid mapUid,
        MapAtmosphereSimulationComponent sim,
        float frameTime)
    {
        sim.Timer += frameTime;

        if (sim.Timer < AtmosTime)
            return AtmosphereProcessingCompletionState.Continue;

        sim.Timer -= AtmosTime;

        switch (sim.State)
        {
            case AtmosphereProcessingState.Revalidate:
                if (!ProcessMapRevalidate(mapUid, sim))
                {
                    sim.ProcessingPaused = true;
                    return AtmosphereProcessingCompletionState.Return;
                }

                sim.ProcessingPaused = false;
                sim.State = AtmosphereProcessingState.ActiveTiles;
                return AtmosphereProcessingCompletionState.Continue;

            case AtmosphereProcessingState.ActiveTiles:
                if (!ProcessMapActiveTiles(mapUid, sim))
                {
                    sim.ProcessingPaused = true;
                    return AtmosphereProcessingCompletionState.Return;
                }

                sim.ProcessingPaused = false;
                sim.State = ExcitedGroups ? AtmosphereProcessingState.ExcitedGroups : AtmosphereProcessingState.Revalidate;
                return AtmosphereProcessingCompletionState.Continue;

            case AtmosphereProcessingState.ExcitedGroups:
                if (!ProcessMapExcitedGroups(mapUid, sim))
                {
                    sim.ProcessingPaused = true;
                    return AtmosphereProcessingCompletionState.Return;
                }

                sim.ProcessingPaused = false;
                sim.State = AtmosphereProcessingState.Revalidate;
                break;
        }

        sim.UpdateCounter++;
        return AtmosphereProcessingCompletionState.Finished;
    }

    private bool ProcessMapRevalidate(EntityUid mapUid, MapAtmosphereSimulationComponent sim)
    {
        if (!sim.ProcessingPaused)
        {
            sim.CurrentRunInvalidatedTiles.Clear();
            sim.CurrentRunInvalidatedTiles.EnsureCapacity(sim.InvalidatedCoords.Count);

            foreach (var worldTile in sim.InvalidatedCoords)
            {
                var tile = GetOrNewMapTile(mapUid, sim, worldTile, out _, invalidateNew: false);
                sim.CurrentRunInvalidatedTiles.Enqueue(tile);
                UpdateMapTileData(mapUid, sim, tile);
            }

            sim.InvalidatedCoords.Clear();

            if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                return false;
        }

        var number = 0;
        while (sim.CurrentRunInvalidatedTiles.TryDequeue(out var tile))
        {
            DebugTools.Assert(sim.Tiles.GetValueOrDefault(tile.GridIndices) == tile);
            UpdateMapAdjacentTiles(mapUid, sim, tile, activate: true);
            EnsureMapTileHasAir(tile);

            if (tile.Air != null)
                AddMapActiveTile(sim, tile);

            InvalidateMapVisuals(mapUid, tile);

            if (number++ < InvalidCoordinatesLagCheckIterations)
                continue;

            number = 0;
            if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                return false;
        }

        return true;
    }

    private bool ProcessMapActiveTiles(EntityUid mapUid, MapAtmosphereSimulationComponent sim)
    {
        if (!sim.ProcessingPaused)
            QueueRunTiles(sim.CurrentRunTiles, sim.ActiveTiles);

        var number = 0;
        while (sim.CurrentRunTiles.TryDequeue(out var tile))
        {
            ProcessMapCell(mapUid, sim, tile, sim.UpdateCounter);

            if (number++ < LagCheckIterations)
                continue;

            number = 0;
            if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                return false;
        }

        return true;
    }

    private bool ProcessMapExcitedGroups(EntityUid mapUid, MapAtmosphereSimulationComponent sim)
    {
        if (!sim.ProcessingPaused)
        {
            sim.CurrentRunExcitedGroups.Clear();
            sim.CurrentRunExcitedGroups.EnsureCapacity(sim.ExcitedGroups.Count);

            foreach (var group in sim.ExcitedGroups)
                sim.CurrentRunExcitedGroups.Enqueue(group);
        }

        var number = 0;
        while (sim.CurrentRunExcitedGroups.TryDequeue(out var excitedGroup))
        {
            excitedGroup.BreakdownCooldown++;
            excitedGroup.DismantleCooldown++;

            if (excitedGroup.BreakdownCooldown > Atmospherics.ExcitedGroupBreakdownCycles)
                MapExcitedGroupSelfBreakdown(mapUid, sim, excitedGroup);
            else if (excitedGroup.DismantleCooldown > Atmospherics.ExcitedGroupsDismantleCycles)
                MapDeactivateGroupTiles(sim, excitedGroup);

            if (number++ < LagCheckIterations)
                continue;

            number = 0;
            if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                return false;
        }

        return true;
    }

    private void ProcessMapCell(
        EntityUid mapUid,
        MapAtmosphereSimulationComponent sim,
        TileAtmosphere tile,
        int fireCount)
    {
        if (tile.Air == null)
            EnsureMapTileHasAir(tile);

        if (tile.Air == null)
        {
            RemoveMapActiveTile(sim, tile);
            return;
        }

        if (tile.ArchivedCycle < fireCount)
            Archive(tile, fireCount);

        tile.CurrentCycle = fireCount;
        var adjacentTileLength = 0;

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection)(1 << i);
            if (tile.AdjacentBits.IsFlagSet(direction))
                adjacentTileLength++;
        }

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection)(1 << i);
            if (!tile.AdjacentBits.IsFlagSet(direction))
                continue;

            var enemyTile = tile.AdjacentTiles[i];

            if (enemyTile != null && enemyTile.Air == null)
            {
                if (enemyTile.NoGridTile)
                    EnsureMapTileHasAir(enemyTile);
                else if (TryComp<GridAtmosphereComponent>(enemyTile.GridIndex, out var gridAtmos)
                         && TryComp<MapGridComponent>(enemyTile.GridIndex, out var grid))
                    EnsureTileHasAir(gridAtmos, grid, enemyTile);
            }

            if (enemyTile?.Air == null)
                continue;

            if (fireCount <= enemyTile.CurrentCycle)
                continue;

            Archive(enemyTile, fireCount);

            var shouldShareAir = false;

            if (ExcitedGroups && tile.ExcitedGroup != null && enemyTile.ExcitedGroup != null)
            {
                if (tile.ExcitedGroup != enemyTile.ExcitedGroup && enemyTile.NoGridTile)
                    MapExcitedGroupMerge(sim, tile.ExcitedGroup, enemyTile.ExcitedGroup);

                shouldShareAir = true;
            }
            else if (CompareExchange(tile, enemyTile) != GasCompareResult.NoExchange)
            {
                if (enemyTile.NoGridTile)
                    AddMapActiveTile(sim, enemyTile);
                else if (TryComp<GridAtmosphereComponent>(enemyTile.GridIndex, out var gridAtmos))
                    AddActiveTile(gridAtmos, enemyTile);

                if (ExcitedGroups)
                {
                    var excitedGroup = tile.ExcitedGroup ?? enemyTile.ExcitedGroup;

                    if (excitedGroup == null)
                    {
                        excitedGroup = new ExcitedGroup();
                        sim.ExcitedGroups.Add(excitedGroup);
                    }

                    if (tile.ExcitedGroup == null)
                        MapExcitedGroupAddTile(excitedGroup, tile);

                    if (enemyTile.ExcitedGroup == null && enemyTile.NoGridTile)
                        MapExcitedGroupAddTile(excitedGroup, enemyTile);
                }

                shouldShareAir = true;
            }

            if (shouldShareAir)
            {
                Share(tile, enemyTile, adjacentTileLength);
                LastShareCheck(tile);
            }
        }

        if (tile.Air != null)
            React(tile.Air, tile);

        InvalidateMapVisuals(mapUid, tile);

        var remove = true;

        if (ExcitedGroups && tile.ExcitedGroup == null && remove)
            RemoveMapActiveTile(sim, tile);
    }

    private void MapExcitedGroupAddTile(ExcitedGroup excitedGroup, TileAtmosphere tile)
    {
        DebugTools.Assert(!excitedGroup.Disposed);
        DebugTools.Assert(tile.ExcitedGroup == null);
        excitedGroup.Tiles.Add(tile);
        tile.ExcitedGroup = excitedGroup;
        MapExcitedGroupResetCooldowns(excitedGroup);
    }

    private void MapExcitedGroupRemoveTile(ExcitedGroup excitedGroup, TileAtmosphere tile)
    {
        DebugTools.Assert(!excitedGroup.Disposed);
        DebugTools.Assert(tile.ExcitedGroup == excitedGroup);
        tile.ExcitedGroup = null;
        excitedGroup.Tiles.Remove(tile);
    }

    private void MapExcitedGroupMerge(
        MapAtmosphereSimulationComponent sim,
        ExcitedGroup ourGroup,
        ExcitedGroup otherGroup)
    {
        DebugTools.Assert(!ourGroup.Disposed);
        DebugTools.Assert(!otherGroup.Disposed);
        DebugTools.Assert(sim.ExcitedGroups.Contains(ourGroup));
        DebugTools.Assert(sim.ExcitedGroups.Contains(otherGroup));

        var ourSize = ourGroup.Tiles.Count;
        var otherSize = otherGroup.Tiles.Count;

        var winner = ourSize > otherSize ? ourGroup : otherGroup;
        var loser = ourSize > otherSize ? otherGroup : ourGroup;

        foreach (var groupTile in loser.Tiles)
        {
            groupTile.ExcitedGroup = winner;
            winner.Tiles.Add(groupTile);
        }

        loser.Tiles.Clear();
        MapExcitedGroupDispose(sim, loser);
        MapExcitedGroupResetCooldowns(winner);
    }

    private void MapExcitedGroupResetCooldowns(ExcitedGroup excitedGroup)
    {
        DebugTools.Assert(!excitedGroup.Disposed);
        excitedGroup.BreakdownCooldown = 0;
        excitedGroup.DismantleCooldown = 0;
    }

    private void MapExcitedGroupSelfBreakdown(
        EntityUid mapUid,
        MapAtmosphereSimulationComponent sim,
        ExcitedGroup excitedGroup)
    {
        DebugTools.Assert(!excitedGroup.Disposed);
        DebugTools.Assert(sim.ExcitedGroups.Contains(excitedGroup));

        var combined = new GasMixture(Atmospherics.CellVolume);
        var tileSize = excitedGroup.Tiles.Count;

        if (excitedGroup.Disposed || tileSize == 0)
        {
            MapExcitedGroupDispose(sim, excitedGroup);
            return;
        }

        foreach (var groupTile in excitedGroup.Tiles)
        {
            if (groupTile?.Air == null)
                continue;

            Merge(combined, groupTile.Air);

            if (!ExcitedGroupsSpaceIsAllConsuming || !groupTile.Space)
                continue;

            combined.Clear();
            break;
        }

        combined.Multiply(1 / (float)tileSize);

        foreach (var groupTile in excitedGroup.Tiles)
        {
            if (groupTile?.Air == null)
                continue;

            groupTile.Air.CopyFrom(combined);
            InvalidateMapVisuals(mapUid, groupTile);
        }

        excitedGroup.BreakdownCooldown = 0;
    }

    private void MapDeactivateGroupTiles(MapAtmosphereSimulationComponent sim, ExcitedGroup excitedGroup)
    {
        foreach (var groupTile in excitedGroup.Tiles)
        {
            groupTile.ExcitedGroup = null;
            RemoveMapActiveTile(sim, groupTile);
        }

        excitedGroup.Tiles.Clear();
    }

    private void MapExcitedGroupDispose(MapAtmosphereSimulationComponent sim, ExcitedGroup excitedGroup)
    {
        if (excitedGroup.Disposed)
            return;

        DebugTools.Assert(sim.ExcitedGroups.Contains(excitedGroup));

        excitedGroup.Disposed = true;
        sim.ExcitedGroups.Remove(excitedGroup);

        foreach (var groupTile in excitedGroup.Tiles)
            groupTile.ExcitedGroup = null;

        excitedGroup.Tiles.Clear();
    }
}
