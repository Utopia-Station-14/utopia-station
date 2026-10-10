using Content.Server.Atmos.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TransformSystem _transformSystem = default!;

    private sealed class ZState
    {
        public TileAtmosphere? Up;
        public TileAtmosphere? Down;
        public bool OpenAir;
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

    #region For Wizdens Atmos

    // ProcessCell: обмен с Z-тайлами после стандартных.
    private void ShareZLevelAtmos(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,
        TransformComponent> ent, TileAtmosphere tile, int adjacentTileLength)
    {
        if ((tile.AdjacentBits & AtmosDirection.Vertical) == 0 || !_zState.TryGetValue(tile, out var state))
        {
            if (IsOpenAirCell(tile) && tile.Air != null)
                ShareToSpaceUp(ent.Comp1, tile, adjacentTileLength);

            return;
        }

        ShareVerticalPair(ent.Comp1, tile, AtmosDirection.Up, state.Up, adjacentTileLength);
        ShareVerticalPair(ent.Comp1, tile, AtmosDirection.Down, state.Down, adjacentTileLength);

        if (state.Up == null && IsOpenAirCell(tile) && tile.Air != null)
            ShareToSpaceUp(ent.Comp1, tile, adjacentTileLength);
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
        if (mapUid == null || (!_zLevels.TryMapOffset(mapUid.Value, 1, out _) && !_zLevels.TryMapOffset(mapUid.Value, -1, out _)))
        {
            BreakVerticalLink(tile, AtmosDirection.Up);
            BreakVerticalLink(tile, AtmosDirection.Down);
            return;
        }

        var below = IsOpenAirCell(tile)
            ? FindZTile(ent.Owner, ent.Comp3, tile.GridIndices, -1)
            : null;
        SetVerticalLink(tile, AtmosDirection.Down, below);

        var above = FindOrCreateCellAbove(ent.Owner, ent.Comp3, tile);
        SetVerticalLink(tile, AtmosDirection.Up, above != null && IsOpenAirCell(above) ? above : null);

        if (above != null)
            RefreshZPeerIfStale(above);
    }

    // Создаёт тайл сверху при наличии стены, чтобы сделать газообмен и помечает его на ревалидацию.
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

    #region Gas Share

    private void ShareVerticalPair(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, AtmosDirection direction,
        TileAtmosphere? other, int neighbours)
    {
        if (other == null)
            return;

        if (!TryGetLiveGridAtmos(other, out var otherAtmos))
        {
            BreakVerticalLink(tile, direction);
            return;
        }

        if (other.Air == null || otherAtmos == null)
            return;

        if (otherAtmos.UpdateCounter <= other.CurrentCycle)
            return;

        if (other.ArchivedCycle < otherAtmos.UpdateCounter)
            Archive(other, otherAtmos.UpdateCounter);

        if (CompareExchange(tile, other) == GasCompareResult.NoExchange)
            return;

        AddActiveTile(otherAtmos, other);
        AddActiveTile(gridAtmosphere, tile);
        EnsureExcitedGroup(gridAtmosphere, tile);
        EnsureExcitedGroup(otherAtmos, other);
        Share(tile, other, neighbours);
        LastShareCheck(tile);

        if (other.ExcitedGroup != null && tile.LastShare > Atmospherics.MinimumAirToSuspend)
            ExcitedGroupResetCooldowns(other.ExcitedGroup);
    }

    private void EnsureExcitedGroup(GridAtmosphereComponent atmos, TileAtmosphere tile)
    {
        if (!ExcitedGroups || tile.ExcitedGroup != null)
            return;

        var group = new ExcitedGroup();
        atmos.ExcitedGroups.Add(group);
        ExcitedGroupAddTile(group, tile);
    }

    private void ShareToSpaceUp(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, int neighbours)
    {
        if (tile.Air == null || tile.Air.TotalMoles <= 0)
            return;

        if (tile.AirArchived == null)
            Archive(tile, tile.CurrentCycle);

        var movedMoles = 0f;
        var absMovedMoles = 0f;

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var thisValue = tile.Air.Moles[i];
            var delta = thisValue / (neighbours + 1);

            if (!(MathF.Abs(delta) >= Atmospherics.GasMinMoles))
                continue;

            tile.Air.Moles[i] -= delta;
            movedMoles += delta;
            absMovedMoles += MathF.Abs(delta);
        }

        if (absMovedMoles <= 0f)
            return;

        tile.LastShare = absMovedMoles;
        AddActiveTile(gridAtmosphere, tile);
    }
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
        if (state.Up == null && state.Down == null && !state.OpenAir)
            _zState.Remove(tile);
    }

    private bool TryGetLiveGridAtmos(TileAtmosphere tile, out GridAtmosphereComponent? atmos)
    {
        if (_gridAtmosQuery.TryComp(tile.GridIndex, out atmos) &&
            atmos.Tiles.TryGetValue(tile.GridIndices, out var current) &&
            ReferenceEquals(current, tile))
        {
            return true;
        }

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

    #region Tiles Lookup

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

    private bool HasZLevelTileBelow(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        Vector2i indices)
        => FindZTile(ent.Owner, ent.Comp3, indices, -1) != null;

    private bool IsZConnectedSpace(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
        => _gridAtmosQuery.TryComp(gridUid, out var atmos) &&
           atmos.Tiles.TryGetValue(indices, out var tile) &&
           (tile.AdjacentBits & AtmosDirection.Vertical) != 0;

    private bool TryUpdateZLevelProtectedTileAir(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        float volume)
        => false;

    #endregion
}
