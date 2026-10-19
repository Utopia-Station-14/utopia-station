/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private ZLevelAtmosSystem _zGas = default!;

    private readonly float[] _zFlows = new float[Atmospherics.TotalNumberOfGases];
    private const int ZMixtureMaxDepth = 10; // <see cref="RespiratorSystem.cs"/> <seealso cref="BarotraumaSystem.cs"/>
    private const bool GoUp = false;
    private const int ZCeilingSearchRadius = 8;
    private const int ZCeilingRescanCycles = 20;

    private sealed class ZState
    {
        public TileAtmosphere? Up;
        public TileAtmosphere? Down;
        public bool OpenAir;
        public bool Ceiling;
        public TileAtmosphere? CeilingTarget;
        public int CeilingScanCycle = -1;
    }

    private readonly Dictionary<TileAtmosphere, ZState> _zState = new();

    private ZState GetOrCreateState(TileAtmosphere tile)
    {
        if (!_zState.TryGetValue(tile, out var state))
        {
            state = new ZState();
            _zState[tile] = state;
        }

        return state;
    }

    private void InitializeZAtmos()
    {
    }

    #region Wizden Atmosphere

    // ProcessCell: обмен с Z-тайлами после стандартных.
    private void ShareZLevelAtmos(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,
        TransformComponent> ent, TileAtmosphere tile, int adjacentTileLength)
    {
        if (!_zState.TryGetValue(tile, out var state))
            return;

        if ((tile.AdjacentBits & AtmosDirection.Vertical) != 0)
        {
            ShareVerticalPair(ent.Comp1, tile, AtmosDirection.Up, state.Up, adjacentTileLength);
            ShareVerticalPair(ent.Comp1, tile, AtmosDirection.Down, state.Down, adjacentTileLength);
        }

        if (!state.Ceiling)
            return;

        if (!ShareToCeilingTarget(ent, tile, state, adjacentTileLength))
            VentToCeiling(ent, tile, adjacentTileLength);
    }

    // ProcessCell: создание коннектов между Z-тайлами.
    private int CountAdjacentWithZ(TileAtmosphere tile)
    {
        var count = System.Numerics.BitOperations.PopCount((uint)(tile.AdjacentBits & AtmosDirection.All));

        if (!_zState.TryGetValue(tile, out var state))
            return count;

        if (state.Up != null)
        {
            tile.AdjacentBits |= AtmosDirection.Up;
            if (state.Up.Air != null)
                count++;
        }

        if (state.Down != null)
        {
            tile.AdjacentBits |= AtmosDirection.Down;
            if (state.Down.Air != null)
                count++;
        }

        return count;
    }

    // ProcessRevalidate: перепроверка коннектов Z-тайлов.
    private void UpdateZAtmosLinks(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,
        TransformComponent> ent, TileAtmosphere tile)
    {
        var mapUid = ent.Comp4.MapUid;

        var isHole = IsHoleCell(ent.Owner, ent.Comp3, tile.GridIndices);
        SetOpenAir(tile, isHole);

        if (mapUid == null || (!_zLevels.TryMapOffset(mapUid.Value, 1, out _) && !_zLevels.TryMapOffset(mapUid.Value, -1, out _)))
        {
            BreakVerticalLink(tile, AtmosDirection.Up);
            BreakVerticalLink(tile, AtmosDirection.Down);
            SetOpenCeiling(tile, false);
            return;
        }

        var belowTile = FindZTile(ent.Owner, ent.Comp3, tile.GridIndices, -1);
        SetVerticalLink(tile, AtmosDirection.Down, IsOpenAirCell(tile) ? belowTile : null);

        var above = FindOrCreateCellAbove(ent.Owner, ent.Comp3, tile);
        SetVerticalLink(tile, AtmosDirection.Up, above != null && IsOpenAirCell(above) ? above : null);
        SetOpenCeiling(tile, IsCeilingOpen(ent.Owner, ent.Comp3, tile));

        if (above != null)
            RefreshZPeerIfStale(above);

        if (belowTile != null)
            RefreshCeilingIfStale(belowTile);
    }

    private bool IsCeilingOpen(EntityUid gridUid, MapGridComponent grid, TileAtmosphere tile)
    {
        if (tile.Air == null || tile.MapAtmosphere)
            return false;

        var mapUid = Transform(gridUid).MapUid;
        if (mapUid == null)
            return false;

        if (!_zLevels.TryMapOffset(mapUid.Value, 1, out _))
            return GoUp && _zLevels.TryMapOffset(mapUid.Value, -1, out _);

        if (FindZTile(gridUid, grid, tile.GridIndices, 1) != null)
            return false;

        if (!TryGetZTarget(gridUid, grid, tile.GridIndices, 1, out var upUid, out var upGrid, out var upIndices))
            return true;

        return upGrid == null || IsHoleCell(upUid, upGrid, upIndices);
    }

    private void SetOpenCeiling(TileAtmosphere tile, bool value)
    {
        if (value)
        {
            var state = GetOrCreateState(tile);
            if (state.Ceiling)
                return;

            state.Ceiling = true;
            state.CeilingTarget = null;
            state.CeilingScanCycle = -1;
            WakeTile(tile);
            return;
        }

        if (_zState.TryGetValue(tile, out var existing) && existing.Ceiling)
        {
            existing.Ceiling = false;
            existing.CeilingTarget = null;
            existing.CeilingScanCycle = -1;
            CleanupState(tile, existing);
        }
    }

    private void RefreshCeilingIfStale(TileAtmosphere peer)
    {
        if (!TryGetLiveGridAtmos(peer, out var peerAtmos) || !TryComp<MapGridComponent>(peer.GridIndex, out var peerGrid))
            return;

        if (IsCeilingOpen(peer.GridIndex, peerGrid, peer) != HasOpenCeiling(peer))
            InvalidateTile((peer.GridIndex, peerAtmos), peer.GridIndices);
    }

    private TileAtmosphere? FindOrCreateCellAbove(EntityUid gridUid, MapGridComponent grid, TileAtmosphere tile)
    {
        var existing = FindZTile(gridUid, grid, tile.GridIndices, 1);
        if (existing != null)
            return existing;

        if (!IsOpenAirCell(tile) && !IsFloor(_map.GetTileRef(gridUid, grid, tile.GridIndices)))
            return null;

        if (!TryGetZTarget(gridUid, grid, tile.GridIndices, 1, out var upUid, out var upGrid, out var upIndices) ||
            !_gridAtmosQuery.TryComp(upUid, out var upAtmos) || !IsHoleCell(upUid, upGrid!, upIndices))
        {
            return null;
        }

        return GetOrNewTile(upUid, upAtmos, upIndices);
    }

    // OnTileChanged: изменение коннектов при удалении/установке тайлов.
    private void InvalidateZAtmosPeers(EntityUid gridUid, Vector2i indices)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        var above = FindZTile(gridUid, gridComp, indices, 1);
        if (above != null && TryGetLiveGridAtmos(above, out var aboveAtmos))
            InvalidateTile((above.GridIndex, aboveAtmos), above.GridIndices);
    }

    // GetContainingMixture: проверка на тайл.
    private bool ShouldTryZLevelProtectedMixture(Entity<TransformComponent?> ent, EntityUid? gridUid, Vector2i position)
    {
        return gridUid != null &&
               _gridAtmosQuery.TryComp(gridUid.Value, out var atmos) &&
               atmos.Tiles.TryGetValue(position, out var tile) &&
               IsOpenAirCell(tile);
    }

    // GetContainingMixture: проверка на тайл для сущности.
    private bool TryGetZLevelProtectedTileMixtureForEntity(Entity<TransformComponent?> ent, bool excite,
        out GasMixture? mixture)
    {
        mixture = null;

        if (ent.Comp?.GridUid is not { } gridUid || !_gridAtmosQuery.TryComp(gridUid, out var atmos))
            return false;

        var position = _transformSystem.GetGridTilePositionOrDefault((ent.Owner, ent.Comp));
        if (!atmos.Tiles.TryGetValue(position, out var tile) || tile.Air == null)
            return false;

        if (excite)
            AddActiveTile(atmos, tile);

        mixture = tile.Air;
        return true;
    }

    #endregion

    #region Gas Sharing

    private void ShareVerticalPair(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, AtmosDirection direction,
        TileAtmosphere? other, int neighbours)
    {
        if (other == null)
            return;

        if (!TryGetLiveGridAtmos(other, out var otherAtmos))
        {
            BreakVerticalLink(tile, direction);
            InvalidateTile((tile.GridIndex, gridAtmosphere), tile.GridIndices);

            return;
        }

        var tileAir = tile.Air;
        var otherAir = other.Air;
        if (tileAir == null || otherAir == null || otherAtmos == null)
            return;

        if (otherAtmos.UpdateCounter <= other.CurrentCycle)
            return;

        if (other.ArchivedCycle < otherAtmos.UpdateCounter)
            Archive(other, otherAtmos.UpdateCounter);

        var tileIsLower = direction == AtmosDirection.Up;
        var lower = tileIsLower ? tileAir : otherAir;
        var upper = tileIsLower ? otherAir : tileAir;

        if (!ExchangeVertical(lower, upper, neighbours, true, out var moved))
            return;

        AddActiveTile(otherAtmos, other);
        AddActiveTile(gridAtmosphere, tile);
        EnsureExcitedGroup(gridAtmosphere, tile);
        EnsureExcitedGroup(otherAtmos, other);

        tile.LastShare = MathF.Max(tile.LastShare, moved);
        LastShareCheck(tile);

        if (other.ExcitedGroup != null && tile.LastShare > Atmospherics.MinimumAirToSuspend)
            ExcitedGroupResetCooldowns(other.ExcitedGroup);
    }


    private bool ExchangeVertical(GasMixture lower, GasMixture upper, int neighbours, bool allowDown, out float moved)
    {
        moved = 0f;

        if (lower.Immutable || upper.Immutable)
            return false;

        var lowerMoles = lower.Moles;
        var upperMoles = upper.Moles;
        var tLower = lower.Temperature;
        var tUpper = upper.Temperature;
        var flows = _zFlows;
        var total = _zGas.ProcessFlows(lowerMoles, tLower, lower.Volume, upperMoles, tUpper, upper.Volume, neighbours, allowDown, flows);
        var moveGas = total >= _zGas.MinMovedMoles;
        var capLower = 0f;
        var capUpper = 0f;
        var capUp = 0f;
        var capDown = 0f;


        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var c = _zGas.GetMolarHeatCapacity(i);
            capLower += lowerMoles[i] * c;
            capUpper += upperMoles[i] * c;

            if (!moveGas)
                continue;

            var x = flows[i];
            if (x > 0f)
                capUp += x * c;
            else
                capDown -= x * c;

            lowerMoles[i] = MathF.Max(0f, lowerMoles[i] - x);
            upperMoles[i] = MathF.Max(0f, upperMoles[i] + x);
        }

        _zGas.MixTemperatures(capLower, tLower, capUpper, tUpper, capUp, capDown, neighbours, allowDown,
            out var newLower, out var newUpper);

        var dynamicMinTempChange = MathF.Max(0.005f, (tLower + tUpper) * 0.0001f);
        if (!moveGas && MathF.Abs(newLower - tLower) < dynamicMinTempChange && MathF.Abs(newUpper - tUpper) < dynamicMinTempChange)
            return false;

        lower.Temperature = MathF.Max(newLower, Atmospherics.TCMB);
        upper.Temperature = MathF.Max(newUpper, Atmospherics.TCMB);
        moved = moveGas ? total : 0f;

        return true;
    }

    private void EnsureExcitedGroup(GridAtmosphereComponent atmos, TileAtmosphere tile)
    {
        if (!ExcitedGroups || tile.ExcitedGroup != null)
            return;

        var group = new ExcitedGroup();
        atmos.ExcitedGroups.Add(group);
        ExcitedGroupAddTile(group, tile);
    }

    private void VentToCeiling(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,
        TransformComponent> ent, TileAtmosphere tile, int neighbours)
    {
        var air = tile.Air;
        if (air == null || air.Immutable)
            return;

        if (air.TotalMoles <= Atmospherics.GasMinMoles)
            return;

        var dynamicVentRatio = Math.Clamp(air.TotalMoles * 0.0005f / (neighbours + 1), 0.1f, 1f);
        var moles = _zGas.Vent(air.Moles, dynamicVentRatio);
        if (moles <= 0f)
            return;

        AddActiveTile(ent.Comp1, tile);
        EnsureExcitedGroup(ent.Comp1, tile);

        tile.LastShare = MathF.Max(tile.LastShare, moles);
        LastShareCheck(tile);
    }

    private bool ShareToCeilingTarget(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,
        TransformComponent> ent, TileAtmosphere tile, ZState state, int neighbours)
    {
        var counter = ent.Comp1.UpdateCounter;

        if (state.CeilingTarget != null && !IsValidCeilingTarget(state.CeilingTarget))
        {
            state.CeilingTarget = null;
            state.CeilingScanCycle = -1;
        }

        if (state.CeilingScanCycle < 0 || counter - state.CeilingScanCycle >= ZCeilingRescanCycles)
        {
            state.CeilingTarget = FindCeilingTarget(ent.Owner, ent.Comp3, tile);
            state.CeilingScanCycle = counter;
        }

        var target = state.CeilingTarget;
        if (target == null || !TryGetLiveGridAtmos(target, out var targetAtmos) || targetAtmos == null)
            return false;

        var air = tile.Air;
        var targetAir = target.Air;
        if (air == null || targetAir == null)
            return true;

        if (!ExchangeVertical(air, targetAir, neighbours, false, out var moved))
            return true;

        AddActiveTile(ent.Comp1, tile);
        AddActiveTile(targetAtmos, target);
        EnsureExcitedGroup(ent.Comp1, tile);
        EnsureExcitedGroup(targetAtmos, target);

        tile.LastShare = MathF.Max(tile.LastShare, moved);
        LastShareCheck(tile);

        if (target.ExcitedGroup != null && tile.LastShare > Atmospherics.MinimumAirToSuspend)
            ExcitedGroupResetCooldowns(target.ExcitedGroup);

        return true;
    }


    private TileAtmosphere? FindCeilingTarget(EntityUid gridUid, MapGridComponent grid, TileAtmosphere tile)
    {
        var mapUid = Transform(gridUid).MapUid;
        if (mapUid == null || !_zLevels.TryMapOffset(mapUid.Value, 1, out var upMap))
            return null;

        var localPos = _mapSystem.GridTileToLocal(gridUid, grid, tile.GridIndices);
        var worldPos = _transformSystem.ToMapCoordinates(localPos).Position;

        const int radiusSq = ZCeilingSearchRadius * ZCeilingSearchRadius;

        TileAtmosphere? best = null;
        var bestDist = int.MaxValue;
        var children = Transform(upMap.Owner).ChildEnumerator;

        while (children.MoveNext(out var childUid))
        {
            if (!_gridAtmosQuery.TryComp(childUid, out var upAtmos) || upAtmos.Tiles.Count == 0 || !TryComp<MapGridComponent>(childUid, out var upGrid))
                continue;

            var upLocal = System.Numerics.Vector2.Transform(worldPos, Transform(childUid).InvLocalMatrix);
            var center = _mapSystem.LocalToTile(childUid, upGrid, new EntityCoordinates(childUid, upLocal));

            for (var dx = -ZCeilingSearchRadius; dx <= ZCeilingSearchRadius; dx++)
            {
                for (var dy = -ZCeilingSearchRadius; dy <= ZCeilingSearchRadius; dy++)
                {
                    var dist = dx * dx + dy * dy;
                    if (dist > radiusSq || dist >= bestDist)
                        continue;

                    if (!upAtmos.Tiles.TryGetValue(new Vector2i(center.X + dx, center.Y + dy), out var candidate) || !IsFreeCeilingTarget(candidate))
                        continue;

                    best = candidate;
                    bestDist = dist;
                }
            }
        }

        return best;
    }

    private static bool IsFreeCeilingTarget(TileAtmosphere tile)
        => tile.Air is { Immutable: false } && !tile.MapAtmosphere;

    private bool IsValidCeilingTarget(TileAtmosphere tile)
        => IsFreeCeilingTarget(tile) && TryGetLiveGridAtmos(tile, out _);

    #endregion

    #region Connections
    private bool IsOpenAirCell(TileAtmosphere tile)
        => _zState.TryGetValue(tile, out var state) && state.OpenAir;

    private void SetOpenAir(TileAtmosphere tile, bool value)
    {
        if (value)
        {
            GetOrCreateState(tile).OpenAir = true;
            return;
        }

        if (_zState.TryGetValue(tile, out var state))
        {
            state.OpenAir = false;
            CleanupState(tile, state);
        }
    }

    private void CleanupState(TileAtmosphere tile, ZState state)
    {
        if (state.Up == null && state.Down == null && !state.OpenAir && !state.Ceiling)
            _zState.Remove(tile);
    }

    private bool TryGetLiveGridAtmos(TileAtmosphere tile, out GridAtmosphereComponent? atmos)
    {
        if (_gridAtmosQuery.TryComp(tile.GridIndex, out atmos) && atmos.Tiles.TryGetValue(tile.GridIndices, out var current) && ReferenceEquals(current, tile))
            return true;

        atmos = null;
        return false;
    }

    private TileAtmosphere? GetStoredLink(TileAtmosphere tile, AtmosDirection direction)
    {
        if (!_zState.TryGetValue(tile, out var state))
            return null;

        return direction == AtmosDirection.Up ? state.Up : state.Down;
    }

    private void SetStoredLink(TileAtmosphere tile, AtmosDirection direction, TileAtmosphere? other)
    {
        if (other == null)
        {
            if (_zState.TryGetValue(tile, out var existing))
            {
                if (direction == AtmosDirection.Up)
                    existing.Up = null;
                else
                    existing.Down = null;

                CleanupState(tile, existing);
            }

            tile.AdjacentBits &= ~direction;
            return;
        }

        var state = GetOrCreateState(tile);
        if (direction == AtmosDirection.Up)
            state.Up = other;
        else
            state.Down = other;

        tile.AdjacentBits |= direction;
    }

    private void BreakVerticalLink(TileAtmosphere tile, AtmosDirection direction)
    {
        var other = GetStoredLink(tile, direction);
        SetStoredLink(tile, direction, null);

        var opposite = direction.GetOpposite();
        if (other != null && ReferenceEquals(GetStoredLink(other, opposite), tile))
            SetStoredLink(other, opposite, null);
    }

    private void SetVerticalLink(TileAtmosphere tile, AtmosDirection direction, TileAtmosphere? other)
    {
        var current = GetStoredLink(tile, direction);
        var opposite = direction.GetOpposite();

        if (ReferenceEquals(current, other))
        {
            if (other != null)
            {
                SetStoredLink(tile, direction, other);
                SetStoredLink(other, opposite, tile);
            }

            return;
        }

        if (current != null)
            BreakVerticalLink(tile, direction);

        if (other == null)
            return;

        if (GetStoredLink(other, opposite) != null)
            BreakVerticalLink(other, opposite);

        SetStoredLink(tile, direction, other);
        SetStoredLink(other, opposite, tile);

        WakeTile(tile);
        WakeTile(other);
    }

    private void WakeTile(TileAtmosphere tile)
    {
        if (_gridAtmosQuery.TryComp(tile.GridIndex, out var atmos))
            AddActiveTile(atmos, tile);
    }

    private void RefreshZPeerIfStale(TileAtmosphere peer)
    {
        if (!TryGetLiveGridAtmos(peer, out var peerAtmos) ||
            !TryComp<MapGridComponent>(peer.GridIndex, out var peerGrid))
        {
            return;
        }

        var shouldBeOpen = IsHoleCell(peer.GridIndex, peerGrid, peer.GridIndices) &&
            HasSupportBelow(peer.GridIndex, peerGrid, peer.GridIndices);

        if (shouldBeOpen != IsOpenAirCell(peer))
            InvalidateTile((peer.GridIndex, peerAtmos), peer.GridIndices);
    }

    #endregion

    #region Tile Lookup

    private bool IsHoleCell(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, indices);
        if (tileRef.Tile.IsEmpty)
            return true;

        return _tileDefinitionManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) &&
               tileDef is ContentTileDefinition { MapAtmosphere: true };
    }

    private bool HasSupportBelow(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!TryGetZTarget(gridUid, grid, indices, -1, out var lowerUid, out var lowerGrid, out var lowerIndices))
            return false;

        if (lowerGrid == null)
            return false;

        if (IsFloor(_map.GetTileRef(lowerUid, lowerGrid, lowerIndices)))
            return true;

        return _gridAtmosQuery.TryComp(lowerUid, out var lowerAtmos) &&
               lowerAtmos.Tiles.TryGetValue(lowerIndices, out var lowerTile) &&
               IsOpenAirCell(lowerTile);
    }

    private bool IsFloor(TileRef tileRef)
    {
        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return false;

        return !(_tileDefinitionManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) &&
                 tileDef is ContentTileDefinition { MapAtmosphere: true });
    }

    private TileAtmosphere? FindZTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset)
    {
        if (!TryGetZTarget(gridUid, grid, indices, offset, out var targetUid, out _, out var targetIndices))
            return null;

        return _gridAtmosQuery.TryComp(targetUid, out var targetAtmos) &&
               targetAtmos.Tiles.TryGetValue(targetIndices, out var targetTile)
            ? targetTile
            : null;
    }

    private bool TryGetZTarget(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset, out EntityUid targetGridUid,
        out MapGridComponent? targetGrid, out Vector2i targetIndices)
    {
        targetGridUid = default;
        targetGrid = null;
        targetIndices = default;

        var mapUid = Transform(gridUid).MapUid;
        if (mapUid == null || !_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap))
            return false;

        var localPos = _mapSystem.GridTileToLocal(gridUid, grid, indices);
        var worldPos = _transformSystem.ToMapCoordinates(localPos).Position;

        if (!_map.TryFindGridAt(targetMap.Owner, worldPos, out targetGridUid, out targetGrid))
            return false;

        var targetXform = Comp<TransformComponent>(targetGridUid);
        var targetLocalPos = System.Numerics.Vector2.Transform(worldPos, targetXform.InvLocalMatrix);
        targetIndices = _mapSystem.LocalToTile(targetGridUid, targetGrid, new EntityCoordinates(targetGridUid, targetLocalPos));
        return true;
    }

    #endregion

    #region Helpers
    private TileAtmosphere? GetZTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset)
        => FindZTile(gridUid, grid, indices, offset);

    private bool HasZLevelTileBelow(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent, Vector2i indices)
        => FindZTile(ent.Owner, ent.Comp3, indices, -1) != null;

    private bool IsZConnectedSpace(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
        => _gridAtmosQuery.TryComp(gridUid, out var atmos) && atmos.Tiles.TryGetValue(indices, out var tile) && (tile.AdjacentBits & AtmosDirection.Vertical) != 0;

    private bool TryUpdateZLevelProtectedTileAir(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent, TileAtmosphere tile, float volume)
        => false;

    private bool HasOpenCeiling(TileAtmosphere tile)
        => _zState.TryGetValue(tile, out var state) && state.Ceiling;

    public GasMixture? GetContainingMixtureZ(EntityUid uid, bool excite = false)
    {
        var mixture = GetContainingMixture(uid, excite: excite);

        if (mixture == null || !IsSpaceMixture(mixture))
            return mixture;

        return GetMixtureBelow(uid, excite) ?? mixture;
    }

    private static bool IsSpaceMixture(GasMixture mixture)
        => mixture.Immutable && mixture.TotalMoles <= Atmospherics.GasMinMoles;

    private GasMixture? GetMixtureBelow(EntityUid uid, bool excite)
    {
        var xform = Transform(uid);
        if (xform.MapUid is not { } currentMap)
            return null;

        var worldPos = _transformSystem.GetWorldPosition(xform);

        for (var i = 0; i < ZMixtureMaxDepth; i++)
        {
            if (!_zLevels.TryMapOffset(currentMap, -1, out var below))
                return null;

            currentMap = below.Owner;

            if (!_map.TryFindGridAt(currentMap, worldPos, out var gridUid, out var grid) || !_gridAtmosQuery.TryComp(gridUid, out var atmos))
                continue;

            var localPos = System.Numerics.Vector2.Transform(worldPos, Transform(gridUid).InvLocalMatrix);
            var indices = _mapSystem.LocalToTile(gridUid, grid, new EntityCoordinates(gridUid, localPos));

            if (!atmos.Tiles.TryGetValue(indices, out var tile) || tile.Air is not { } air || IsSpaceMixture(air))
                continue;

            if (excite)
                AddActiveTile(atmos, tile);

            return air;
        }

        return null;
    }

    #endregion
}
