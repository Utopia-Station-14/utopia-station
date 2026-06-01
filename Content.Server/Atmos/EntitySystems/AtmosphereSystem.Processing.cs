using Content.Server.Atmos.Components;
using Content.Server.Atmos.Piping.Components;
using Content.Shared.Atmos;
using JetBrains.Annotations;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems
{
    public sealed partial class AtmosphereSystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        private readonly Stopwatch _simulationStopwatch = new();

        /// <summary>
        ///     Check current execution time every n instances processed.
        /// </summary>
        private const int LagCheckIterations = 30;

        /// <summary>
        ///     Check current execution time every n instances processed.
        /// </summary>
        private const int InvalidCoordinatesLagCheckIterations = 50;

        private int _currentRunAtmosphereIndex;
        private bool _simulationPaused;

        private TileAtmosphere GetOrNewTile(
            EntityUid owner,
            GridAtmosphereComponent atmosphere,
            Vector2i index,
            out bool existing,
            bool invalidateNew = true)
        {
            var tile = atmosphere.Tiles.GetOrNew(index, out existing);
            if (existing)
                return tile;

            if (invalidateNew)
                atmosphere.InvalidatedCoords.Add(index);

            tile.GridIndex = owner;
            tile.GridIndices = index;
            return tile;
        }

        private readonly List<Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>> _currentRunAtmosphere = new();

        /// <summary>
        ///     Revalidates all invalid coordinates in a grid atmosphere.
        ///     I.e., process any tiles that have had their airtight blockers modified.
        /// </summary>
        /// <param name="ent">The grid atmosphere in question.</param>
        /// <returns>Whether the process succeeded or got paused due to time constrains.</returns>
        private bool ProcessRevalidate(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            if (ent.Comp4.MapUid == null)
            {
                Log.Error($"Attempted to process atmosphere on a map-less grid? Grid: {ToPrettyString(ent)}");
                return true;
            }

            var (uid, atmosphere, visuals, grid, xform) = ent;
            var volume = GetVolumeForTiles(grid);
            TryComp(xform.MapUid, out MapAtmosphereComponent? mapAtmos);

            if (!atmosphere.ProcessingPaused)
            {
                atmosphere.CurrentRunInvalidatedTiles.Clear();
                atmosphere.CurrentRunInvalidatedTiles.EnsureCapacity(atmosphere.InvalidatedCoords.Count);
                foreach (var indices in atmosphere.InvalidatedCoords)
                {
                    var tile = GetOrNewTile(uid, atmosphere, indices, out _, invalidateNew: false);
                    atmosphere.CurrentRunInvalidatedTiles.Enqueue(tile);

                    // Update tile.IsSpace and tile.MapAtmosphere, and tile.AirtightData.
                    UpdateTileData(ent, mapAtmos, tile);
                }
                atmosphere.InvalidatedCoords.Clear();

                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                    return false;
            }

            var number = 0;
            while (atmosphere.CurrentRunInvalidatedTiles.TryDequeue(out var tile))
            {
                DebugTools.Assert(atmosphere.Tiles.GetValueOrDefault(tile.GridIndices) == tile);
                UpdateAdjacentTiles(ent, tile, activate: true, mapAtmos, volume, atmosphere.CurrentRunInvalidatedTiles);
                UpdateTileAir(ent, tile, volume);

                if (tile.Air != null)
                    AddActiveTile(atmosphere, tile);

                InvalidateVisuals(ent, tile);

                if (number++ < InvalidCoordinatesLagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                    return false;
            }

            TrimDisconnectedMapTiles(ent);
            return true;
        }

        /// <summary>
        /// This method queued a tile and all of its neighbours up for processing by <see cref="TrimDisconnectedMapTiles"/>.
        /// </summary>
        public void QueueTileTrim(GridAtmosphereComponent atmos, TileAtmosphere tile)
        {
            if (!tile.TrimQueued)
            {
                tile.TrimQueued = true;
                atmos.PossiblyDisconnectedTiles.Add(tile);
            }

            for (var i = 0; i < Atmospherics.Directions; i++)
            {
                var direction = (AtmosDirection) (1 << i);
                var indices = tile.GridIndices.Offset(direction);
                if (atmos.Tiles.TryGetValue(indices, out var adj)
                    && adj.NoGridTile
                    && !adj.TrimQueued)
                {
                    adj.TrimQueued = true;
                    atmos.PossiblyDisconnectedTiles.Add(adj);
                }
            }
        }

        /// <summary>
        /// Tiles in a <see cref="GridAtmosphereComponent"/> are either grid-tiles, or they they should be are tiles
        /// adjacent to grid-tiles that represent the map's atmosphere. This method trims any map-tiles that are no longer
        /// adjacent to any grid-tiles.
        /// </summary>
        private void TrimDisconnectedMapTiles(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            // Do not delete off-grid atmos cells; gas must be able to exist and spread everywhere.
            ent.Comp1.PossiblyDisconnectedTiles.Clear();
        }

        /// <summary>
        /// Checks whether a tile has a corresponding grid-tile, or whether it is a "map" tile. Also checks whether the
        /// tile should be considered "space"
        /// </summary>
        private void UpdateTileData(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
            MapAtmosphereComponent? mapAtmos,
            TileAtmosphere tile)
        {
            var idx = tile.GridIndices;
            var spaceDef = (ContentTileDefinition) _tileDefinitionManager[ContentTileDefinition.SpaceID];

            if (_map.TryGetTile(ent.Comp3, idx, out var gTile) && !gTile.IsEmpty)
            {
                var contentDef = (ContentTileDefinition) _tileDefinitionManager[gTile.TypeId];
                tile.ThermalConductivity = contentDef.ThermalConductivity;
                tile.HeatCapacity = contentDef.HeatCapacity;
            }
            else
            {
                tile.ThermalConductivity = spaceDef.ThermalConductivity;
                tile.HeatCapacity = spaceDef.HeatCapacity;
            }

            // Every atmos cell is a normal gas cell (floor, empty, space turf, off-grid void).
            tile.NoGridTile = false;
            tile.Space = false;

            UpdateAirtightData(ent.Owner, ent.Comp1, ent.Comp3, tile);

            if (tile.MapAtmosphere)
                RemoveMapAtmos(ent.Comp1, tile);
            else if (tile.Air?.Immutable == true)
            {
                tile.Air = null;
                tile.AirArchived = null;
                tile.ArchivedCycle = 0;
                tile.LastShare = 0f;
            }
        }

        /// <summary>
        /// Ensures a tile has a mutable gas mixture. Empty and space tiles are not special-cased.
        /// </summary>
        private void EnsureTileHasAir(
            GridAtmosphereComponent atmos,
            MapGridComponent? grid,
            TileAtmosphere tile,
            float volume = 0)
        {
            if (tile.MapAtmosphere)
                RemoveMapAtmos(atmos, tile);

            if (tile.Air is { Immutable: false })
            {
                tile.Space = false;
                return;
            }

            if (tile.Air?.Immutable == true)
            {
                tile.Air = null;
                tile.AirArchived = null;
                tile.ArchivedCycle = 0;
                tile.LastShare = 0f;
            }

            if (volume <= 0)
            {
                volume = grid != null
                    ? GetVolumeForTiles(grid)
                    : Atmospherics.CellVolume;
            }

            tile.Air ??= new GasMixture(volume) { Temperature = Atmospherics.T20C };
            tile.Space = false;
        }

        private void RemoveMapAtmos(GridAtmosphereComponent atmos, TileAtmosphere tile)
        {
            DebugTools.Assert(tile.MapAtmosphere);
            tile.MapAtmosphere = false;
            atmos.MapTiles.Remove(tile);
            tile.Air = null;
            tile.AirArchived = null;
            tile.ArchivedCycle = 0;
            tile.LastShare = 0f;
            tile.Space = false;
        }

        /// <summary>
        /// Check whether a grid-tile should have an air mixture, and give it one if it doesn't already have one.
        /// </summary>
        private void UpdateTileAir(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
            TileAtmosphere tile,
            float volume)
        {
            var hadAir = tile.Air != null;
            EnsureTileHasAir(ent.Comp1, ent.Comp3, tile, volume);

            if (tile.AirtightData.FixVacuum)
                GridFixTileVacuum(tile);

            if (!hadAir && tile.Air != null)
                NotifyDeviceTileChanged((ent.Owner, ent.Comp1, ent.Comp3), tile.GridIndices);
        }

        /// <summary>
        /// Creates or refreshes a simulated atmos cell immediately.
        /// Required for empty grid tiles (open space) which otherwise have no <see cref="TileAtmosphere"/> entry.
        /// </summary>
        private void SyncAtmosTile(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
            Vector2i indices,
            MapAtmosphereComponent? mapAtmos = null)
        {
            var (uid, atmos, _, grid, xform) = ent;

            if (mapAtmos == null && xform.MapUid != null)
                TryComp(xform.MapUid, out mapAtmos);

            var tile = GetOrNewTile(uid, atmos, indices, out _, invalidateNew: false);
            UpdateTileData(ent, mapAtmos, tile);

            var volume = GetVolumeForTiles(grid);
            UpdateAdjacentTiles(ent, tile, activate: true, mapAtmos, volume, null);
            UpdateTileAir(ent, tile, volume);

            if (tile.Air != null)
                AddActiveTile(atmos, tile);

            InvalidateVisuals((ent.Owner, ent.Comp2), indices);
        }

        /// <summary>
        /// Public entry for other systems (overlay, vents) to force an atmos cell on empty/open tiles.
        /// </summary>
        [PublicAPI]
        public void EnsureAtmosTileForIndices(EntityUid gridUid, Vector2i indices, bool excite = true)
        {
            if (!TryComp(gridUid, out GridAtmosphereComponent? atmos) ||
                !TryComp(gridUid, out MapGridComponent? grid) ||
                !TryComp(gridUid, out GasTileOverlayComponent? overlay) ||
                !TryComp(gridUid, out TransformComponent? xform))
            {
                return;
            }

            SyncAtmosTile((gridUid, atmos, overlay, grid, xform), indices);

            if (!excite || !atmos.Tiles.TryGetValue(indices, out var tile) || tile.Air == null)
                return;

            AddActiveTile(atmos, tile);
        }

        private void QueueRunTiles(
            Queue<TileAtmosphere> queue,
            HashSet<TileAtmosphere> tiles)
        {

            queue.Clear();
            queue.EnsureCapacity(tiles.Count);
            foreach (var tile in tiles)
            {
                queue.Enqueue(tile);
            }
        }

        private bool ProcessTileEqualize(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            var atmosphere = ent.Comp1;
            if (!atmosphere.ProcessingPaused)
                QueueRunTiles(atmosphere.CurrentRunTiles, atmosphere.ActiveTiles);

            var number = 0;
            while (atmosphere.CurrentRunTiles.TryDequeue(out var tile))
            {
                EqualizePressureInZone(ent, tile, atmosphere.UpdateCounter);

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessActiveTiles(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            var atmosphere = ent.Comp1;
            if(!atmosphere.ProcessingPaused)
                QueueRunTiles(atmosphere.CurrentRunTiles, atmosphere.ActiveTiles);

            var number = 0;
            while (atmosphere.CurrentRunTiles.TryDequeue(out var tile))
            {
                ProcessCell(ent, tile, atmosphere.UpdateCounter);

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessExcitedGroups(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            var gridAtmosphere = ent.Comp1;
            if (!gridAtmosphere.ProcessingPaused)
            {
                gridAtmosphere.CurrentRunExcitedGroups.Clear();
                gridAtmosphere.CurrentRunExcitedGroups.EnsureCapacity(gridAtmosphere.ExcitedGroups.Count);
                foreach (var group in gridAtmosphere.ExcitedGroups)
                {
                    gridAtmosphere.CurrentRunExcitedGroups.Enqueue(group);
                }
            }

            var number = 0;
            while (gridAtmosphere.CurrentRunExcitedGroups.TryDequeue(out var excitedGroup))
            {
                excitedGroup.BreakdownCooldown++;
                excitedGroup.DismantleCooldown++;

                if (excitedGroup.BreakdownCooldown > Atmospherics.ExcitedGroupBreakdownCycles)
                    ExcitedGroupSelfBreakdown(ent, excitedGroup);
                else if (excitedGroup.DismantleCooldown > Atmospherics.ExcitedGroupsDismantleCycles)
                    DeactivateGroupTiles(gridAtmosphere, excitedGroup);

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessHighPressureDelta(Entity<GridAtmosphereComponent> ent)
        {
            var atmosphere = ent.Comp;
            if (!atmosphere.ProcessingPaused)
                QueueRunTiles(atmosphere.CurrentRunTiles, atmosphere.HighPressureDelta);

            // Note: This is still processed even if space wind is turned off since this handles playing the sounds.

            var number = 0;
            var bodies = GetEntityQuery<PhysicsComponent>();
            var xforms = GetEntityQuery<TransformComponent>();
            var metas = GetEntityQuery<MetaDataComponent>();
            var pressureQuery = GetEntityQuery<MovedByPressureComponent>();

            while (atmosphere.CurrentRunTiles.TryDequeue(out var tile))
            {
                HighPressureMovements(ent, tile, bodies, xforms, pressureQuery, metas);
                tile.PressureDifference = 0f;
                tile.LastPressureDirection = tile.PressureDirection;
                tile.PressureDirection = AtmosDirection.Invalid;
                tile.PressureSpecificTarget = null;
                atmosphere.HighPressureDelta.Remove(tile);

                if (number++ < LagCheckIterations)
                    continue;
                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessHotspots(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            var atmosphere = ent.Comp1;
            if(!atmosphere.ProcessingPaused)
                QueueRunTiles(atmosphere.CurrentRunTiles, atmosphere.HotspotTiles);

            var number = 0;
            while (atmosphere.CurrentRunTiles.TryDequeue(out var hotspot))
            {
                ProcessHotspot(ent, hotspot);

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessSuperconductivity(GridAtmosphereComponent atmosphere)
        {
            if(!atmosphere.ProcessingPaused)
                QueueRunTiles(atmosphere.CurrentRunTiles, atmosphere.SuperconductivityTiles);

            var number = 0;
            while (atmosphere.CurrentRunTiles.TryDequeue(out var superconductivity))
            {
                Superconduct(atmosphere, superconductivity);

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Processes all entities with a <see cref="DeltaPressureComponent"/>, doing damage to them
        /// depending on certain pressure differential conditions.
        /// </summary>
        /// <returns>True if we've finished processing all entities that required processing this run,
        /// otherwise, false.</returns>
        private bool ProcessDeltaPressure(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent)
        {
            var atmosphere = ent.Comp1;
            var count = atmosphere.DeltaPressureEntities.Count;
            if (!atmosphere.ProcessingPaused)
            {
                atmosphere.DeltaPressureCursor = 0;
                atmosphere.DeltaPressureDamageResults.Clear();
            }

            var remaining = count - atmosphere.DeltaPressureCursor;
            var batchSize = Math.Max(50, DeltaPressureParallelProcessPerIteration);
            var toProcess = Math.Min(batchSize, remaining);

            var timeCheck1 = 0;
            while (atmosphere.DeltaPressureCursor < count)
            {
                var job = new DeltaPressureParallelJob(this,
                    atmosphere,
                    atmosphere.DeltaPressureCursor,
                    DeltaPressureParallelBatchSize);
                _parallel.ProcessNow(job, toProcess);

                atmosphere.DeltaPressureCursor += toProcess;

                if (timeCheck1++ < LagCheckIterations)
                    continue;

                timeCheck1 = 0;
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                    return false;
            }

            var timeCheck2 = 0;
            while (atmosphere.DeltaPressureDamageResults.TryDequeue(out var result))
            {
                PerformDamage(result.Ent,
                    result.Pressure,
                    result.DeltaPressure);

                if (timeCheck2++ < LagCheckIterations)
                    continue;

                timeCheck2 = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private bool ProcessPipeNets(GridAtmosphereComponent atmosphere)
        {
            if (!atmosphere.ProcessingPaused)
            {
                atmosphere.CurrentRunPipeNet.Clear();
                atmosphere.CurrentRunPipeNet.EnsureCapacity(atmosphere.PipeNets.Count);
                foreach (var net in atmosphere.PipeNets)
                {
                    atmosphere.CurrentRunPipeNet.Enqueue(net);
                }
            }

            var number = 0;
            while (atmosphere.CurrentRunPipeNet.TryDequeue(out var pipenet))
            {
                pipenet.Update();

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        /**
         * UpdateProcessing() takes a different number of calls to go through all of atmos
         * processing depending on what options are enabled. This returns the actual effective time
         * between atmos updates that devices actually experience.
         */
        public float RealAtmosTime()
        {
            int num = (int)AtmosphereProcessingState.NumStates;
            if (!MonstermosEqualization)
                num--;
            if (!ExcitedGroups)
                num--;
            if (!DeltaPressureDamage)
                num--;
            if (!Superconduction)
                num--;
            return num * AtmosTime;
        }

        private bool ProcessAtmosDevices(
            Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
            Entity<MapAtmosphereComponent?> map)
        {
            var atmosphere = ent.Comp1;
            if (!atmosphere.ProcessingPaused)
            {
                atmosphere.CurrentRunAtmosDevices.Clear();
                atmosphere.CurrentRunAtmosDevices.EnsureCapacity(atmosphere.AtmosDevices.Count);
                foreach (var device in atmosphere.AtmosDevices)
                {
                    atmosphere.CurrentRunAtmosDevices.Enqueue(device);
                }
            }

            var time = _gameTiming.CurTime;
            var number = 0;
            var ev = new AtmosDeviceUpdateEvent(RealAtmosTime(), (ent, ent.Comp1, ent.Comp2), map);
            while (atmosphere.CurrentRunAtmosDevices.TryDequeue(out var device))
            {
                RaiseLocalEvent(device, ref ev);
                device.Comp.LastProcess = time;

                if (number++ < LagCheckIterations)
                    continue;

                number = 0;
                // Process the rest next time.
                if (_simulationStopwatch.Elapsed.TotalMilliseconds >= AtmosMaxProcessTime)
                {
                    return false;
                }
            }

            return true;
        }

        private void UpdateProcessing(float frameTime)
        {
            _simulationStopwatch.Restart();

            if (!_simulationPaused)
            {
                _currentRunAtmosphereIndex = 0;
                _currentRunAtmosphere.Clear();

                var query = EntityQueryEnumerator<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>();
                while (query.MoveNext(out var uid, out var atmos, out var overlay, out var grid, out var xform ))
                {
                    _currentRunAtmosphere.Add((uid, atmos, overlay, grid, xform));
                }
            }

            // We set this to true just in case we have to stop processing due to time constraints.
            _simulationPaused = true;

            for (; _currentRunAtmosphereIndex < _currentRunAtmosphere.Count; _currentRunAtmosphereIndex++)
            {
                var ent = _currentRunAtmosphere[_currentRunAtmosphereIndex];
                var (owner, atmosphere, visuals, grid, xform) = ent;

                if (xform.MapUid == null
                    || TerminatingOrDeleted(xform.MapUid.Value)
                    || xform.MapID == MapId.Nullspace)
                {
                    Log.Error($"Attempted to process atmos without a map? Entity: {ToPrettyString(owner)}. Map: {ToPrettyString(xform?.MapUid)}. MapId: {xform?.MapID}");
                    continue;
                }

                if (atmosphere.LifeStage >= ComponentLifeStage.Stopping || Paused(owner) || !atmosphere.Simulated)
                    continue;

                var map = new Entity<MapAtmosphereComponent?>(xform.MapUid.Value, _mapAtmosQuery.CompOrNull(xform.MapUid.Value));

                var completionState = ProcessAtmosphere(ent, map, frameTime);

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

            // We finished processing all atmospheres successfully, therefore we won't be paused next tick.
            _simulationPaused = false;
        }

        /// <summary>
        /// Processes a <see cref="GridAtmosphereComponent"/> through its processing stages.
        /// </summary>
        /// <param name="ent">The entity to process.</param>
        /// <param name="mapAtmosphere">The <see cref="MapAtmosphereComponent"/> belonging to the
        /// <see cref="GridAtmosphereComponent"/>'s map.</param>
        /// <param name="frameTime">The elapsed time since the last frame.</param>
        /// <returns>An <see cref="AtmosphereProcessingCompletionState"/> that represents the completion state.</returns>
        private AtmosphereProcessingCompletionState ProcessAtmosphere(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
            Entity<MapAtmosphereComponent?> mapAtmosphere,
            float frameTime)
        {
            // They call me the deconstructor the way i be deconstructing it
            // and by it, i mean... my entity
            var (owner, atmosphere, visuals, grid, xform) = ent;

            atmosphere.Timer += frameTime;

            if (atmosphere.Timer < AtmosTime)
                return AtmosphereProcessingCompletionState.Continue;

            // We subtract it so it takes lost time into account.
            atmosphere.Timer -= AtmosTime;

            switch (atmosphere.State)
            {
                case AtmosphereProcessingState.Revalidate:
                    if (!ProcessRevalidate(ent))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;

                    // Next state depends on whether monstermos equalization is enabled or not.
                    // Note: We do this here instead of on the tile equalization step to prevent ending it early.
                    //       Therefore, a change to this CVar might only be applied after that step is over.
                    atmosphere.State = MonstermosEqualization
                        ? AtmosphereProcessingState.TileEqualize
                        : AtmosphereProcessingState.ActiveTiles;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.TileEqualize:
                    if (!ProcessTileEqualize(ent))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = AtmosphereProcessingState.ActiveTiles;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.ActiveTiles:
                    if (!ProcessActiveTiles(ent))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    // Next state depends on whether excited groups are enabled or not.
                    atmosphere.State = ExcitedGroups ? AtmosphereProcessingState.ExcitedGroups : AtmosphereProcessingState.HighPressureDelta;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.ExcitedGroups:
                    if (!ProcessExcitedGroups(ent))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = AtmosphereProcessingState.HighPressureDelta;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.HighPressureDelta:
                    if (!ProcessHighPressureDelta((ent, ent)))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = DeltaPressureDamage
                        ? AtmosphereProcessingState.DeltaPressure
                        : AtmosphereProcessingState.Hotspots;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.DeltaPressure:
                    if (!ProcessDeltaPressure(ent))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = AtmosphereProcessingState.Hotspots;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.Hotspots:
                    if (!ProcessHotspots(ent))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    // Next state depends on whether superconduction is enabled or not.
                    // Note: We do this here instead of on the tile equalization step to prevent ending it early.
                    //       Therefore, a change to this CVar might only be applied after that step is over.
                    atmosphere.State = Superconduction
                        ? AtmosphereProcessingState.Superconductivity
                        : AtmosphereProcessingState.PipeNet;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.Superconductivity:
                    if (!ProcessSuperconductivity(atmosphere))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = AtmosphereProcessingState.PipeNet;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.PipeNet:
                    if (!ProcessPipeNets(atmosphere))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = AtmosphereProcessingState.AtmosDevices;
                    return AtmosphereProcessingCompletionState.Continue;
                case AtmosphereProcessingState.AtmosDevices:
                    if (!ProcessAtmosDevices(ent, mapAtmosphere))
                    {
                        atmosphere.ProcessingPaused = true;
                        return AtmosphereProcessingCompletionState.Return;
                    }

                    atmosphere.ProcessingPaused = false;
                    atmosphere.State = AtmosphereProcessingState.Revalidate;

                    // We reached the end of this atmosphere's update tick. Break out of the switch.
                    break;
            }

            atmosphere.UpdateCounter++;

            return AtmosphereProcessingCompletionState.Finished;
        }
    }

    /// <summary>
    /// An enum representing the completion state of a <see cref="GridAtmosphereComponent"/>'s processing steps.
    /// The processing of a <see cref="GridAtmosphereComponent"/> spans over multiple stages and sticks,
    /// with the method handling the processing having multiple return types.
    /// </summary>
    public enum AtmosphereProcessingCompletionState : byte
    {
        /// <summary>
        /// Method is returning, ex. due to delegating processing to the next tick.
        /// </summary>
        Return,

        /// <summary>
        /// Method is continuing, ex. due to finishing a single processing stage.
        /// </summary>
        Continue,

        /// <summary>
        /// Method is finished with the GridAtmosphere.
        /// </summary>
        Finished,
    }

    public enum AtmosphereProcessingState : byte
    {
        Revalidate,
        TileEqualize,
        ActiveTiles,
        ExcitedGroups,
        HighPressureDelta,
        DeltaPressure,
        Hotspots,
        Superconductivity,
        PipeNet,
        AtmosDevices,
        NumStates
    }
}
