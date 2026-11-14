using System.Runtime.CompilerServices;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems
{
    public sealed partial class AtmosphereSystem
    {
        // Utopia-Tweak : Atmos-Updt
        private static readonly int DirUp = AtmosDirection.Up.ToIndex();
        private static readonly int DirDown = AtmosDirection.Down.ToIndex();
        private static readonly int HorizontalDirs = DirUp;
        private static readonly int ThermalDirs = DirDown + 1;

        private const float ZConductivityFactor = 1f;
        private const int ZThermalRescanCycles = 20;

        private sealed class ZThermalLinks
        {
            public TileAtmosphere? Up;
            public TileAtmosphere? Down;
            public int ScanCycle = -1;
        }

        private readonly Dictionary<TileAtmosphere, ZThermalLinks> _zThermal = new();
        private readonly List<TileAtmosphere> _superconductBuffer = new();
        private readonly HashSet<TileAtmosphere> _superconductSnapshot = new();

        private sealed class ThermalOverride
        {
            public static readonly ThermalOverride None = new(null, null, 10000f, 0.1f, 10f);

            public readonly float? Conductivity;
            public readonly float? HeatCapacity;
            public readonly float WallReferenceHeatCapacity;
            public readonly float WallWarmupMinFactor;
            public readonly float WallWarmupMaxFactor;

            public ThermalOverride(float? conductivity, float? heatCapacity, float refCap, float minFactor, float maxFactor)
            {
                Conductivity = conductivity;
                HeatCapacity = heatCapacity;
                WallReferenceHeatCapacity = refCap;
                WallWarmupMinFactor = minFactor;
                WallWarmupMaxFactor = maxFactor;
            }
        }

        private readonly ConditionalWeakTable<TileAtmosphere, ThermalOverride> _thermalOverrides = new();

        private void InitializeSuperconductivity()
        {
            SubscribeLocalEvent<ThermalConductorComponent, ComponentStartup>((uid, _, _) => InvalidateThermalOverride(uid));
            SubscribeLocalEvent<ThermalConductorComponent, ComponentShutdown>((uid, _, _) => InvalidateThermalOverride(uid));
            SubscribeLocalEvent<ThermalConductorComponent, AnchorStateChangedEvent>((uid, _, _) => InvalidateThermalOverride(uid));
        }

        private void InvalidateThermalOverride(EntityUid uid)
        {
            if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid is not { } gridUid)
                return;

            if (!_gridAtmosQuery.TryComp(gridUid, out var atmos))
                return;

            var position = _transformSystem.GetGridTilePositionOrDefault((uid, xform));
            if (atmos.Tiles.TryGetValue(position, out var tile))
                _thermalOverrides.Remove(tile);
        }

        private ThermalOverride GetThermalOverride(TileAtmosphere tile)
        {
            if (_thermalOverrides.TryGetValue(tile, out var cached))
                return cached;

            var result = ThermalOverride.None;
            if (TryComp<MapGridComponent>(tile.GridIndex, out var grid))
            {
                var found = false;
                var minConductivity = float.MaxValue;
                var capacitySum = 0f;
                var hasCapacity = false;

                var refCap = 10000f;
                var minFactor = 0.1f;
                var maxFactor = 10f;

                var enumerator = _mapSystem.GetAnchoredEntitiesEnumerator(tile.GridIndex, grid, tile.GridIndices);
                while (enumerator.MoveNext(out var uid))
                {
                    if (!TryComp(uid, out ThermalConductorComponent? conductor))
                        continue;

                    found = true;
                    minConductivity = MathF.Min(minConductivity, conductor.ThermalConductivity);
                    refCap = conductor.WallReferenceHeatCapacity;
                    minFactor = conductor.WallWarmupMinFactor;
                    maxFactor = conductor.WallWarmupMaxFactor;

                    if (conductor.HeatCapacity is { } capacity)
                    {
                        capacitySum += capacity;
                        hasCapacity = true;
                    }
                }

                if (found)
                    result = new ThermalOverride(minConductivity, hasCapacity ? capacitySum : null, refCap, minFactor, maxFactor);
            }

            _thermalOverrides.Add(tile, result);
            return result;
        }

        private float GetTileConductivity(TileAtmosphere tile)
            => Math.Clamp(GetThermalOverride(tile).Conductivity ?? tile.ThermalConductivity, 0f, 1f);

        private float GetTileHeatCapacity(TileAtmosphere tile)
            => GetThermalOverride(tile).HeatCapacity ?? tile.HeatCapacity;

        private float GetConductionThreshold(TileAtmosphere tile)
        {
            if (tile.Air != null)
                return Atmospherics.MinimumTemperatureForSuperconduction;

            var thermalOverride = GetThermalOverride(tile);
            var factor = Math.Clamp(GetTileHeatCapacity(tile) / thermalOverride.WallReferenceHeatCapacity, thermalOverride.WallWarmupMinFactor, thermalOverride.WallWarmupMaxFactor);
            return Atmospherics.T20C + (Atmospherics.MinimumTemperatureForSuperconduction - Atmospherics.T20C) * factor;
        }

        public void ProcessSuperconductivityTick(GridAtmosphereComponent gridAtmosphere)
        {
            if (!Superconduction)
            {
                foreach (var tile in gridAtmosphere.SuperconductivityTiles)
                    _zThermal.Remove(tile);

                gridAtmosphere.SuperconductivityTiles.Clear();
                return;
            }

            _superconductBuffer.Clear();
            _superconductSnapshot.Clear();

            foreach (var tile in gridAtmosphere.SuperconductivityTiles)
            {
                _superconductBuffer.Add(tile);
                _superconductSnapshot.Add(tile);
            }

            foreach (var tile in _superconductBuffer)
            {
                if (!TryGetLiveGridAtmos(tile, out _))
                {
                    gridAtmosphere.SuperconductivityTiles.Remove(tile);
                    _zThermal.Remove(tile);
                    continue;
                }

                Superconduct(gridAtmosphere, tile);
            }

            _superconductBuffer.Clear();
            _superconductSnapshot.Clear();
        }

        private void Superconduct(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile)
        {
            if (tile.Air is { Immutable: true })
            {
                FinishSuperconduction(gridAtmosphere, tile, 0f);
                return;
            }

            for (var dir = 0; dir < ThermalDirs; dir++)
            {
                var vertical = dir >= HorizontalDirs;
                var adjacent = GetThermalNeighbor(gridAtmosphere, tile, dir, out var adjacentGrid);
                if (adjacent == null || adjacentGrid == null || adjacent.Air is { Immutable: true })
                    continue;

                var k = GetConductivity(tile, adjacent, vertical);
                if (k <= 0f)
                    continue;

                k *= GetPairWeight(gridAtmosphere, adjacentGrid, adjacent);

                var airBlocked = vertical
                    ? GetStoredLink(tile, dir.ToAtmosDirection()) == null
                    : tile.AdjacentTiles[dir] == null;

                ConductPair(gridAtmosphere, tile, adjacentGrid, adjacent, k, airBlocked);

                if (GetHeatTemperature(adjacent) >= GetConductionThreshold(adjacent))
                    ConsiderSuperconductivity(adjacentGrid, adjacent);
            }

            RadiateToSpace(gridAtmosphere, tile);
            FinishSuperconduction(gridAtmosphere, tile);
        }

        #region Neighbors
        private TileAtmosphere? GetThermalNeighbor(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, int dir, out GridAtmosphereComponent? neighborGrid)
        {
            neighborGrid = null;
            if (dir < HorizontalDirs)
            {
                var indices = tile.GridIndices.Offset(dir.ToAtmosDirection());
                if (!gridAtmosphere.Tiles.TryGetValue(indices, out var neighbor))
                    return null;

                neighborGrid = gridAtmosphere;
                return neighbor;
            }

            return GetVerticalThermalNeighbor(gridAtmosphere, tile, dir, out neighborGrid);
        }

        private TileAtmosphere? GetVerticalThermalNeighbor(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, int dir, out GridAtmosphereComponent? neighborGrid)
        {
            neighborGrid = null;
            if (!_zThermal.TryGetValue(tile, out var links))
            {
                links = new ZThermalLinks();
                _zThermal[tile] = links;
            }

            if (links.ScanCycle < 0 || gridAtmosphere.UpdateCounter - links.ScanCycle >= ZThermalRescanCycles)
            {
                links.ScanCycle = gridAtmosphere.UpdateCounter;

                if (TryComp<MapGridComponent>(tile.GridIndex, out var grid))
                {
                    links.Up = FindZTile(tile.GridIndex, grid, tile.GridIndices, 1);
                    links.Down = FindZTile(tile.GridIndex, grid, tile.GridIndices, -1);
                }
                else
                {
                    links.Up = null;
                    links.Down = null;
                }
            }

            var neighbor = dir == DirUp ? links.Up : links.Down;
            if (neighbor == null || !TryGetLiveGridAtmos(neighbor, out neighborGrid) || neighborGrid == null)
            {
                links.ScanCycle = -1;
                neighborGrid = null;
                return null;
            }

            return neighbor;
        }

        private float GetPairWeight(GridAtmosphereComponent gridAtmosphere, GridAtmosphereComponent adjacentGrid, TileAtmosphere adjacent)
        {
            var neighborProcessed = ReferenceEquals(adjacentGrid, gridAtmosphere)
                ? _superconductSnapshot.Contains(adjacent)
                : adjacentGrid.SuperconductivityTiles.Contains(adjacent);

            return neighborProcessed ? 0.5f : 1f;
        }

        private float GetConductivity(TileAtmosphere a, TileAtmosphere b, bool vertical)
        {
            var k = MathF.Min(GetTileConductivity(a), GetTileConductivity(b));
            if (vertical)
                k *= ZConductivityFactor;

            return Math.Clamp(k, 0f, 1f);
        }
        #endregion

        #region Conduction
        private void ConductPair(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, GridAtmosphereComponent adjacentGrid,
            TileAtmosphere adjacent, float k, bool airBlocked)
        {
            if (tile.Air == null && adjacent.Air == null)
                TemperatureShareMutualSolid(tile, adjacent, k);
            else if (tile.Air != null && adjacent.Air == null)
            {
                TemperatureShareOpenToSolid(tile, adjacent, k);
                AddActiveTile(gridAtmosphere, tile);
            }
            else if (tile.Air == null)
            {
                TemperatureShareOpenToSolid(adjacent, tile, k);
                AddActiveTile(adjacentGrid, adjacent);
            }
            else if (airBlocked)
            {
                if (tile.ArchivedCycle < gridAtmosphere.UpdateCounter)
                    Archive(tile, gridAtmosphere.UpdateCounter);

                if (adjacent.ArchivedCycle < adjacentGrid.UpdateCounter)
                    Archive(adjacent, adjacentGrid.UpdateCounter);

                TemperatureShare(tile, adjacent, MathF.Min(k, Atmospherics.WindowHeatTransferCoefficient));
                AddActiveTile(gridAtmosphere, tile);
                AddActiveTile(adjacentGrid, adjacent);
            }
        }

        private void TemperatureShareOpenToSolid(TileAtmosphere gasTile, TileAtmosphere solid, float k)
        {
            var capacity = GetTileHeatCapacity(solid);
            if (gasTile.Air == null || capacity <= 0f)
                return;

            solid.Temperature = TemperatureShare(gasTile, k, solid.Temperature, capacity);
        }

        private void TemperatureShareMutualSolid(TileAtmosphere tile, TileAtmosphere other, float k)
        {
            var tileCapacity = GetTileHeatCapacity(tile);
            var otherCapacity = GetTileHeatCapacity(other);

            if (tileCapacity <= 0f || otherCapacity <= 0f)
                return;

            var deltaTemperature = tile.Temperature - other.Temperature;
            if (MathF.Abs(deltaTemperature) <= Atmospherics.MinimumTemperatureDeltaToConsider)
                return;

            var heat = k * deltaTemperature * (tileCapacity * otherCapacity / (tileCapacity + otherCapacity));
            tile.Temperature -= heat / tileCapacity;
            other.Temperature += heat / otherCapacity;
        }

        public void RadiateToSpace(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile)
        {
            var capacity = GetTileHeatCapacity(tile);
            if (capacity <= 0f || tile.Temperature <= Atmospherics.T0C)
                return;

            var exposed = 0;
            for (var dir = 0; dir < HorizontalDirs; dir++)
            {
                var neighbor = GetThermalNeighbor(gridAtmosphere, tile, dir, out _);
                if (neighbor == null || (neighbor.Air != null && IsSpaceMixture(neighbor.Air)))
                    exposed++;
            }

            if (exposed == 0)
                return;

            var deltaTemperature = tile.Temperature - Atmospherics.TCMB;
            if (deltaTemperature <= Atmospherics.MinimumTemperatureDeltaToConsider)
                return;

            var exposure = exposed / (float) HorizontalDirs;
            var heat = GetTileConductivity(tile) * deltaTemperature * exposure *
                       (capacity * Atmospherics.HeatCapacityVacuum /
                        (capacity + Atmospherics.HeatCapacityVacuum));

            tile.Temperature = MathF.Max(Atmospherics.TCMB, tile.Temperature - heat / capacity);
        }
        #endregion

        #region Tracking
        public bool ConsiderSuperconductivity(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile)
        {
            if (GetTileConductivity(tile) == 0f || !Superconduction)
                return false;

            gridAtmosphere.SuperconductivityTiles.Add(tile);
            return true;
        }

        public bool ConsiderSuperconductivity(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, bool starting)
        {
            if (!Superconduction)
                return false;

            if (tile.Air == null || tile.Air.Temperature < (starting
                    ? Atmospherics.MinimumTemperatureStartSuperConduction
                    : Atmospherics.MinimumTemperatureForSuperconduction))
                return false;

            return !(GetHeatCapacity(tile.Air) < Atmospherics.MCellWithRatio)
                   && ConsiderSuperconductivity(gridAtmosphere, tile);
        }

        public void FinishSuperconduction(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile)
        {
            var capacity = GetTileHeatCapacity(tile);
            if (tile.Air != null && capacity > 0f)
                tile.Temperature = TemperatureShare(tile, GetTileConductivity(tile), tile.Temperature, capacity);

            FinishSuperconduction(gridAtmosphere, tile, GetHeatTemperature(tile));
        }

        public void FinishSuperconduction(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, float temperature)
        {
            if (temperature < GetConductionThreshold(tile))
            {
                gridAtmosphere.SuperconductivityTiles.Remove(tile);
                _zThermal.Remove(tile);
            }
        }

        private static float GetHeatTemperature(TileAtmosphere tile)
        {
            return tile.Air != null
                ? MathF.Max(tile.Temperature, tile.Air.Temperature)
                : tile.Temperature;
        }
        // Utopia-Tweak : Atmos-Updt
        #endregion
    }
}
