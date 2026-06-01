using Content.Server.Atmos.Components;
using Content.Server.Atmos.Reactions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.Reactions;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private void InitializeGridAtmosphere()
    {
        SubscribeLocalEvent<GridAtmosphereComponent, ComponentInit>(OnGridAtmosphereInit);
        SubscribeLocalEvent<GridAtmosphereComponent, ComponentStartup>(OnGridAtmosphereStartup);
        SubscribeLocalEvent<GridAtmosphereComponent, ComponentRemove>(OnAtmosphereRemove);
        SubscribeLocalEvent<GridAtmosphereComponent, GridSplitEvent>(OnGridSplit);

        #region Atmos API Subscriptions

        SubscribeLocalEvent<GridAtmosphereComponent, IsSimulatedGridMethodEvent>(GridIsSimulated);
        SubscribeLocalEvent<GridAtmosphereComponent, GetAllMixturesMethodEvent>(GridGetAllMixtures);
        SubscribeLocalEvent<GridAtmosphereComponent, ReactTileMethodEvent>(GridReactTile);
        SubscribeLocalEvent<GridAtmosphereComponent, HotspotExtinguishMethodEvent>(GridHotspotExtinguish);
        SubscribeLocalEvent<GridAtmosphereComponent, IsHotspotActiveMethodEvent>(GridIsHotspotActive);

        #endregion
    }

    private void OnAtmosphereRemove(EntityUid uid, GridAtmosphereComponent component, ComponentRemove args)
    {
        for (var i = 0; i < _currentRunAtmosphere.Count; i++)
        {
            if (_currentRunAtmosphere[i].Owner != uid)
                continue;

            _currentRunAtmosphere.RemoveAt(i);
            if (_currentRunAtmosphereIndex > i)
                _currentRunAtmosphereIndex--;
        }
    }

    private void OnGridAtmosphereInit(EntityUid uid, GridAtmosphereComponent component, ComponentInit args)
    {
        EnsureComp<GasTileOverlayComponent>(uid);
        foreach (var tile in component.Tiles.Values)
        {
            tile.GridIndex = uid;
        }
    }

    private void OnGridAtmosphereStartup(EntityUid uid, GridAtmosphereComponent component, ComponentStartup args)
    {
        if (!TryComp(uid, out MapGridComponent? mapGrid))
            return;

        InvalidateAllTiles((uid, mapGrid, component));
    }

    private void OnGridSplit(EntityUid uid, GridAtmosphereComponent originalGridAtmos, ref GridSplitEvent args)
    {
        foreach (var newGrid in args.NewGrids)
        {
            // Make extra sure this is a valid grid.
            if (!TryComp(newGrid, out MapGridComponent? mapGrid))
                continue;

            // If the new split grid has an atmosphere already somehow, use that. Otherwise, add a new one.
            if (!TryComp(newGrid, out GridAtmosphereComponent? newGridAtmos))
                newGridAtmos = AddComp<GridAtmosphereComponent>(newGrid);

            // We assume the tiles on the new grid have the same coordinates as they did on the old grid...
            var enumerator = _mapSystem.GetAllTilesEnumerator(newGrid, mapGrid);

            while (enumerator.MoveNext(out var tile))
            {
                var indices = tile.Value.GridIndices;

                // This split event happens *before* the spaced tiles have been invalidated, therefore we can still
                // access their gas data. On the next atmos update tick, these tiles will be spaced. Poof!
                if (!originalGridAtmos.Tiles.TryGetValue(indices, out var tileAtmosphere))
                    continue;

                // The new grid atmosphere has been initialized, meaning it has all the needed TileAtmospheres...
                if (!newGridAtmos.Tiles.TryGetValue(indices, out var newTileAtmosphere))
                    // Let's be honest, this is really not gonna happen, but just in case...!
                    continue;

                // Copy a bunch of data over... Not great, maybe put this in TileAtmosphere?
                newTileAtmosphere.Air = tileAtmosphere.Air?.Clone();
                newTileAtmosphere.Hotspot = tileAtmosphere.Hotspot;
                newTileAtmosphere.HeatCapacity = tileAtmosphere.HeatCapacity;
                newTileAtmosphere.Temperature = tileAtmosphere.Temperature;
                newTileAtmosphere.PressureDifference = tileAtmosphere.PressureDifference;
                newTileAtmosphere.PressureDirection = tileAtmosphere.PressureDirection;

                // TODO ATMOS: Somehow force GasTileOverlaySystem to perform an update *right now, right here.*
                // The reason why is that right now, gas will flicker until the next GasTileOverlay update.
                // That looks bad, of course. We want to avoid that! Anyway that's a bit more complicated so out of scope.

                // Invalidate the tile, it's redundant but redundancy is good! Also HashSet so really, no duplicates.
                originalGridAtmos.InvalidatedCoords.Add(indices);
                newGridAtmos.InvalidatedCoords.Add(indices);
            }
        }
    }

    private void GridIsSimulated(EntityUid uid, GridAtmosphereComponent component, ref IsSimulatedGridMethodEvent args)
    {
        if (args.Handled)
            return;

        args.Simulated = component.Simulated;
        args.Handled = true;
    }

    private void GridGetAllMixtures(EntityUid uid, GridAtmosphereComponent component,
        ref GetAllMixturesMethodEvent args)
    {
        if (args.Handled)
            return;

        IEnumerable<GasMixture> EnumerateMixtures(EntityUid gridUid, GridAtmosphereComponent grid, bool invalidate)
        {
            foreach (var (indices, tile) in grid.Tiles)
            {
                if (tile.Air == null)
                    continue;

                if (invalidate)
                {
                    //var ev = new InvalidateTileMethodEvent(gridUid, indices);
                    //GridInvalidateTile(gridUid, grid, ref ev);
                    AddActiveTile(grid, tile);
                }

                yield return tile.Air;
            }
        }

        // Return the enumeration over all the tiles in the atmosphere.
        args.Mixtures = EnumerateMixtures(uid, component, args.Excite);
        args.Handled = true;
    }

    private void GridReactTile(EntityUid uid, GridAtmosphereComponent component, ref ReactTileMethodEvent args)
    {
        if (args.Handled)
            return;

        if (!component.Tiles.TryGetValue(args.Tile, out var tile))
            return;

        args.Result = tile.Air is { } air ? React(air, tile) : ReactionResult.NoReaction;
        args.Handled = true;
    }

    /// <summary>
    /// Update array of adjacent tiles and the adjacency flags.
    /// </summary>
    private void UpdateAdjacentTiles(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        bool activate = false,
        MapAtmosphereComponent? mapAtmos = null,
        float volume = 0,
        Queue<TileAtmosphere>? revalidateQueue = null)
    {
        var uid = ent.Owner;
        var atmos = ent.Comp1;
        if (activate)
            AddActiveTile(atmos, tile);

        tile.AdjacentBits = AtmosDirection.Invalid;
        var mapUid = ent.Comp4.MapUid;
        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var direction = (AtmosDirection)(1 << i);
            var adjacentIndices = tile.GridIndices.Offset(direction);
            var adjacentWorld = GridTileToWorldTile(uid, ent.Comp3, tile.GridIndices).Offset(direction);

            TileAtmosphere? adjacent;
            var existed = false;

            // Prefer same-grid cells when the grid still owns that tile coordinate.
            if (_map.TryGetTile(ent.Comp3, adjacentIndices, out _) || atmos.Tiles.ContainsKey(adjacentIndices))
            {
                adjacent = GetOrNewTile(uid, atmos, adjacentIndices, out existed, invalidateNew: revalidateQueue == null);

                if (!existed)
                {
                    if (revalidateQueue != null)
                    {
                        UpdateTileData(ent, mapAtmos, adjacent);
                        revalidateQueue.Enqueue(adjacent);
                    }
                    else
                    {
                        SyncAtmosTile(ent, adjacentIndices, mapAtmos);
                        adjacent = atmos.Tiles.GetValueOrDefault(adjacentIndices);
                    }
                }
            }
            else if (mapUid != null
                     && TryResolveAtmosTileAtWorld(mapUid.Value, adjacentWorld, create: true, out adjacent, out var ownerUid, out var onMapSim)
                     && adjacent != null)
            {
                if (onMapSim && TryComp<MapAtmosphereSimulationComponent>(ownerUid, out var mapSim))
                {
                    mapSim.InvalidatedCoords.Add(adjacentWorld);
                    EnsureMapTileHasAir(adjacent);
                }
                else if (!onMapSim && TryComp<GridAtmosphereComponent>(ownerUid, out var otherAtmos)
                         && TryComp<MapGridComponent>(ownerUid, out var otherGrid)
                         && TryComp<GasTileOverlayComponent>(ownerUid, out var otherOverlay)
                         && TryComp<TransformComponent>(ownerUid, out var otherXform))
                {
                    var otherEnt = (ownerUid, otherAtmos, otherOverlay, otherGrid, otherXform);
                    UpdateTileData(otherEnt, mapAtmos, adjacent);
                    EnsureTileHasAir(otherAtmos, otherGrid, adjacent);
                }
            }
            else
            {
                continue;
            }

            if (adjacent == null)
                continue;

            if (activate)
                AddActiveTile(atmos, adjacent);

            var oppositeIndex = i.ToOppositeIndex();
            var oppositeDirection = (AtmosDirection)(1 << oppositeIndex);

            if (IsAdjacentBlocked(tile, direction, adjacent, oppositeDirection))
            {
                UnlinkAdjacentTiles(tile, adjacent, i, oppositeIndex, direction, oppositeDirection);
            }
            else
            {
                LinkAdjacentTiles(tile, adjacent, i, oppositeIndex, direction, oppositeDirection);

                if (activate)
                {
                    if (adjacent.NoGridTile && mapUid != null && TryComp<MapAtmosphereSimulationComponent>(mapUid, out var mapSim))
                        AddMapActiveTile(mapSim, adjacent);
                    else if (TryComp<GridAtmosphereComponent>(adjacent.GridIndex, out var adjAtmos))
                        AddActiveTile(adjAtmos, adjacent);
                }
            }

            DebugTools.Assert(!(tile.AdjacentBits.IsFlagSet(direction) ^
                                adjacent.AdjacentBits.IsFlagSet(oppositeDirection)));

            if (!adjacent.AdjacentBits.IsFlagSet(adjacent.MonstermosInfo.CurrentTransferDirection))
                adjacent.MonstermosInfo.CurrentTransferDirection = AtmosDirection.Invalid;
        }

        if (!tile.AdjacentBits.IsFlagSet(tile.MonstermosInfo.CurrentTransferDirection))
            tile.MonstermosInfo.CurrentTransferDirection = AtmosDirection.Invalid;
    }

    private (GasMixture Air, bool IsSpace) GetDefaultMapAtmosphere(MapAtmosphereComponent? map)
    {
        if (map == null)
            return (GasMixture.SpaceGas, true);

        var air = map.Mixture;
        DebugTools.Assert(air.Immutable);
        return (air, map.Space);
    }

    /// <summary>
    ///     Mutable copy of the map atmosphere for an <c>isSpace</c> / <see cref="Content.Shared.Maps.ContentTileDefinition.MapAtmosphere"/> tile.
    ///     Map-level mixtures stay immutable; per-tile copies participate in LINDA.
    /// </summary>
    private GasMixture CreateMutableMapAtmosphere(MapAtmosphereComponent? map, float volume)
    {
        var template = map?.Mixture ?? GasMixture.SpaceGas;
        if (volume <= 0)
            volume = template.Volume > 0 ? template.Volume : Atmospherics.CellVolume;

        return new GasMixture(template) { Volume = volume };
    }

    private void GridHotspotExtinguish(EntityUid uid, GridAtmosphereComponent component,
        ref HotspotExtinguishMethodEvent args)
    {
        if (args.Handled)
            return;

        if (!component.Tiles.TryGetValue(args.Tile, out var tile))
            return;

        tile.Hotspot = new Hotspot();
        args.Handled = true;

        //var ev = new InvalidateTileMethodEvent(uid, args.Tile);
        //GridInvalidateTile(uid, component, ref ev);
        AddActiveTile(component, tile);
    }

    private void GridIsHotspotActive(EntityUid uid, GridAtmosphereComponent component,
        ref IsHotspotActiveMethodEvent args)
    {
        if (args.Handled)
            return;

        if (!component.Tiles.TryGetValue(args.Tile, out var tile))
            return;

        args.Result = tile.Hotspot.Valid;
        args.Handled = true;
    }

    private void GridFixTileVacuum(TileAtmosphere tile)
    {
        DebugTools.AssertNotNull(tile.Air);
        DebugTools.Assert(tile.Air?.Immutable == false);
        tile.AirArchived = null;
        tile.ArchivedCycle = 0;

        var count = 0;
        foreach (var adj in tile.AdjacentTiles)
        {
            if (adj?.Air != null)
                count++;
        }

        if (count == 0)
            return;

        var ratio = 1f / count;
        var totalTemperature = 0f;

        foreach (var adj in tile.AdjacentTiles)
        {
            if (adj?.Air == null)
                continue;

            totalTemperature += adj.Temperature;

            // TODO ATMOS. Why is this removing and then re-adding air to the neighbouring tiles?
            // Is it some rounding issue to do with Atmospherics.GasMinMoles? because otherwise this is just unnecessary.
            // if we get rid of this, then this could also just add moles and then multiply by ratio at the end, rather
            // than having to iterate over adjacent tiles twice.

            // Remove a bit of gas from the adjacent ratio...
            var mix = adj.Air.RemoveRatio(ratio);

            // And merge it to the new tile air.
            Merge(tile.Air, mix);

            // Return removed gas to its original mixture.
            Merge(adj.Air, mix);
        }

        // New temperature is the arithmetic mean of the sum of the adjacent temperatures...
        tile.Air.Temperature = totalTemperature / count;
    }

    /// <summary>
    ///     Repopulates all tiles on a grid atmosphere.
    /// </summary>
    public void InvalidateAllTiles(Entity<MapGridComponent?, GridAtmosphereComponent?> entity)
    {
        var (uid, grid, atmos) = entity;
        if (!Resolve(uid, ref grid, ref atmos))
            return;

        foreach (var indices in atmos.Tiles.Keys)
        {
            atmos.InvalidatedCoords.Add(indices);
        }

        var enumerator = _map.GetAllTilesEnumerator(uid, grid, ignoreEmpty: false);
        while (enumerator.MoveNext(out var tile))
        {
            atmos.InvalidatedCoords.Add(tile.Value.GridIndices);
        }
    }

    public TileRef GetTileRef(TileAtmosphere tile)
    {
        if (!TryComp(tile.GridIndex, out MapGridComponent? grid))
            return default;
        _map.TryGetTileRef(tile.GridIndex, grid, tile.GridIndices, out var tileRef);
        return tileRef;
    }
}
