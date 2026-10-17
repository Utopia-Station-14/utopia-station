using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Maps;
using Robust.Shared.Utility;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Server.GameObjects;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private TransformSystem _transformSystem = default!;

    private const float TransferAmount = 1f;

    private void InitializeZAtmos()
    {
    }

    /// <summary>
    /// AtmosphereSystem.LINDA метод ProcessCell.
    /// </summary>
    private void ShareZLevelAtmos(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile, int fireCount)
    {
        var indices = tile.GridIndices;
        if (!IsZConnectedSpace(ent.Owner, ent.Comp3, indices))
            return;

        // Обрабатываем обмен газов с этажами.
        ProcessZTile(ent, tile, indices, 1, fireCount);
        ProcessZTile(ent, tile, indices, -1, fireCount);
    }

    /// <summary>
    /// AtmosphereSystem.LINDA метод ProcessRevalidate.
    /// </summary>
    private void RefreshZAtmosTransferCandidates(Entity<GridAtmosphereComponent, GasTileOverlayComponent,
        MapGridComponent, TransformComponent> ent, Vector2i indices)
    {
        // При необходимости обновляет кэш или связи для измененного тайла.
    }

    /// <summary>
    /// AtmosphereSystem.Processing ProcessRevalidate.
    /// </summary>
    private void ActivateZAtmosTransferCandidate(GridAtmosphereComponent atmos, TileAtmosphere tile)
    {
        // Обновление атмос-тайлов.
    }

    /// <summary>
    /// AtmosphereSystem.Processing метод UpdateProcessing.
    /// </summary>
    private void RunZAtmosProcessing()
    {
        // Вызывается в начале каждого тика.
    }

    /// <summary>
    /// AtmosphereSystem.Grid метод GetContainingMixture.
    /// </summary>
    private bool ShouldTryZLevelProtectedMixture(Entity<TransformComponent?> ent, EntityUid? gridUid, Vector2i position)
    {
        return gridUid != null &&
               TryComp<MapGridComponent>(gridUid.Value, out var gridComp) &&
               IsZConnectedSpace(gridUid.Value, gridComp, position);
    }

    /// <summary>
    /// AtmosphereSystem.Grid метод GetContainingMixture.
    /// </summary>
    private bool TryGetZLevelProtectedTileMixtureForEntity(Entity<TransformComponent?> ent, bool excite, [NotNullWhen(true)] out GasMixture? mixture)
    {
        mixture = null;

        if (ent.Comp?.GridUid == null || !TryComp<MapGridComponent>(ent.Comp.GridUid.Value, out var gridComp))
            return false;

        var position = _transformSystem.GetGridTilePositionOrDefault((ent.Owner, ent.Comp));
        var targetTile = GetZTile(ent.Comp.GridUid.Value, gridComp, position, 1)
                      ?? GetZTile(ent.Comp.GridUid.Value, gridComp, position, -1);

        if (targetTile?.Air == null)
            return false;

        mixture = targetTile.Air;
        return true;
    }

    /// <summary>
    /// AtmosphereSystem.Processing метод для предотвращения удаления NoGridTile.
    /// </summary>
    private bool HasAnySolidZLevelTileBelow(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var down = GetZTile(gridUid, grid, indices, -1);
        if (down?.Air != null)
            return true;

        var up = GetZTile(gridUid, grid, indices, 1);
        return up?.Air != null;
    }

    /// <summary>
    /// AtmosphereSystem.Processing метод UpdateTileData.
    /// </summary>
    private bool HasZLevelTileBelow(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent, Vector2i indices)
        => GetZTile(ent.Owner, ent.Comp3, indices, -1) != null;

    /// <summary>
    /// AtmosphereSystem.Processing метод UpdateTileAir.
    /// </summary>
    private bool TryUpdateZLevelProtectedTileAir(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent, TileAtmosphere tile, float volume)
        => false;

    /// <summary>
    /// AtmosphereSystem метод OnTileChanged.
    /// </summary>
    private void InvalidateZAtmosPeers(EntityUid gridUid, Vector2i indices)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        var up = GetZTile(gridUid, gridComp, indices, 1);
        if (up != null && TryComp<GridAtmosphereComponent>(up.GridIndex, out var upAtmos))
            InvalidateTile((up.GridIndex, upAtmos), up.GridIndices);

        var down = GetZTile(gridUid, gridComp, indices, -1);
        if (down != null && TryComp<GridAtmosphereComponent>(down.GridIndex, out var downAtmos))
            InvalidateTile((down.GridIndex, downAtmos), down.GridIndices);
    }

    /// <summary>
    /// Проверяет, является ли тайл открытым пространством, способным соединяться с другими этажами.
    /// </summary>
    private bool IsZConnectedSpace(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!TryComp<GridAtmosphereComponent>(gridUid, out var atmos) ||
            !atmos.Tiles.TryGetValue(indices, out var tile) ||
            (!tile.Space && !tile.MapAtmosphere && !tile.NoGridTile))
            return false;

        var up = GetZTile(gridUid, grid, indices, 1);
        if (up?.Air != null)
            return true;

        var down = GetZTile(gridUid, grid, indices, -1);
        return down?.Air != null;
    }

    /// <summary>
    /// Основная логика перемещения газов между этажами.
    /// </summary>
    private void ProcessZTile(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile, Vector2i indices, int offset, int fireCount)
    {
        var mapUid = ent.Comp4.MapUid;
        if (mapUid == null || !_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap))
            return;

        // Проверяем движение вниз.
        if (offset == -1 && IsTileBlockingZAir(ent.Owner, ent.Comp3, indices))
            return;

        var localPos = _mapSystem.GridTileToLocal(ent.Owner, ent.Comp3, indices);
        var worldPos = _transformSystem.ToMapCoordinates(localPos).Position;

        EntityUid targetGridUid = default;
        MapGridComponent? targetGridComp = null;
        Vector2i targetIndices = default;
        bool foundValidTarget = false;

        // Если мы идем вверх.
        if (offset == 1)
        {
            if (_map.TryFindGridAt(targetMap.Owner, worldPos, out var directGridUid, out var directGridComp))
            {
                var directGridXform = Comp<TransformComponent>(directGridUid);
                var directLocalPos = Vector2.Transform(worldPos, directGridXform.InvLocalMatrix);
                var directIndices = _mapSystem.LocalToTile(directGridUid, directGridComp, new EntityCoordinates(directGridUid, directLocalPos));

                if (!IsTileBlockingZAir(directGridUid, directGridComp, directIndices))
                {
                    targetGridUid = directGridUid;
                    targetGridComp = directGridComp;
                    targetIndices = directIndices;
                    foundValidTarget = true;
                }
            }

            // Если над тайлом ничего нет, ищем в радиусе 1 тайла пол на верхнем этаже.
            if (!foundValidTarget)
            {
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0)
                            continue;

                        var offsetLocalPos = _mapSystem.GridTileToLocal(ent.Owner, ent.Comp3, indices + new Vector2i(x, y));
                        var offsetWorldPos = _transformSystem.ToMapCoordinates(offsetLocalPos).Position;

                        if (_map.TryFindGridAt(targetMap.Owner, offsetWorldPos, out var nGridUid, out var nGridComp))
                        {
                            var nGridXform = Comp<TransformComponent>(nGridUid);
                            var nLocalPos = Vector2.Transform(offsetWorldPos, nGridXform.InvLocalMatrix);
                            var nIndices = _mapSystem.LocalToTile(nGridUid, nGridComp, new EntityCoordinates(nGridUid, nLocalPos));

                            if (!IsTileBlockingZAir(nGridUid, nGridComp, nIndices))
                            {
                                targetGridUid = nGridUid;
                                targetGridComp = nGridComp;
                                targetIndices = nIndices;
                                foundValidTarget = true;
                                break;
                            }
                        }
                    }
                    if (foundValidTarget)
                        break;
                }
            }

            // Если вообще ничего не нашли в радиусе - спускаем газ в вакуум.
            if (!foundValidTarget)
            {
                HandleSpaceLeak(ent, tile);
                return;
            }
        }
        else // Логика для движения вниз.
        {
            if (!_map.TryFindGridAt(targetMap.Owner, worldPos, out targetGridUid, out targetGridComp))
            {
                HandleSpaceLeak(ent, tile);
                return;
            }

            var targetGridXform = Comp<TransformComponent>(targetGridUid);
            var targetLocalPos = Vector2.Transform(worldPos, targetGridXform.InvLocalMatrix);
            targetIndices = _mapSystem.LocalToTile(targetGridUid, targetGridComp, new EntityCoordinates(targetGridUid, targetLocalPos));

            foundValidTarget = true;
        }

        if (!foundValidTarget || targetGridComp == null)
            return;

        var targetTile = GetZTileAtPosition(targetGridUid, targetGridComp, targetIndices);

        if (targetTile?.MapAtmosphere == true || targetTile == null || targetTile.Space)
        {
            HandleSpaceLeak(ent, tile);
            return;
        }

        if (targetTile.Air == null)
            return;

        if (fireCount > targetTile.CurrentCycle)
            Archive(targetTile, fireCount);

        if (!TryComp<GridAtmosphereComponent>(targetGridUid, out var targetGridAtmos))
            return;

        if (CompareExchange(tile, targetTile) != GasCompareResult.NoExchange)
        {
            AddActiveTileForZLevel(targetGridAtmos, targetTile);
            AddActiveTileForZLevel(ent.Comp1, tile);

            var difference = Share(tile, targetTile, 1);
            if (MathF.Abs(difference) > Atmospherics.MinimumMolesDeltaToMove)
            {
                if (tile.ExcitedGroup != null)
                    tile.ExcitedGroup.DismantleCooldown = 0;

                if (targetTile.ExcitedGroup != null)
                    targetTile.ExcitedGroup.DismantleCooldown = 0;
            }
        }

        LastShareCheck(tile);
        LastShareCheck(targetTile);
    }

    /// <summary>
    /// Вспомогательный метод для активации тайла на любом гриде.
    /// </summary>
    private void AddActiveTileForZLevel(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile)
    {
        if (tile.Air == null || tile.Excited)
            return;

        tile.Excited = true;
        if (!gridAtmosphere.ActiveTiles.Contains(tile))
            gridAtmosphere.ActiveTiles.Add(tile);
    }

    /// <summary>
    /// Обработка утечки газа в отверстия.
    /// </summary>
    private void HandleSpaceLeak(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent, TileAtmosphere tile)
    {
        if (tile.Air == null || tile.Air.TotalMoles <= 0)
            return;

        var molesToRem = tile.Air.TotalMoles * TransferAmount;
        tile.Air.Remove(molesToRem);

        AddActiveTile(ent.Comp1, tile);
        if (tile.ExcitedGroup != null)
            tile.ExcitedGroup.DismantleCooldown = 0;

        LastShareCheck(tile);

        if (tile.Air.TotalMoles > Atmospherics.MinimumMolesDeltaToMove)
            InvalidateTile((ent.Owner, ent.Comp1), tile.GridIndices);
    }

    /// <summary>
    /// Проверяет, блокирует ли пол на данном тайле движение газа по этажам.
    /// </summary>
    private bool IsTileBlockingZAir(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, indices);

        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return false;

        if (_tileDefinitionManager.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) &&
            tileDef is ContentTileDefinition contentDef &&
            contentDef.MapAtmosphere)
        {
            return false;
        }

        return true;
    }

    private TileAtmosphere? GetZTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset)
    {
        var mapUid = Transform(gridUid).MapUid;
        if (mapUid == null || !_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap))
            return null;

        var localPos = _mapSystem.GridTileToLocal(gridUid, grid, indices);
        var worldPos = _transformSystem.ToMapCoordinates(localPos).Position;

        if (!_map.TryFindGridAt(targetMap.Owner, worldPos, out var targetGridUid, out var targetGridComp))
            return null;

        var targetGridXform = Comp<TransformComponent>(targetGridUid);
        var targetLocalPos = Vector2.Transform(worldPos, targetGridXform.InvLocalMatrix);
        var targetIndices = _mapSystem.LocalToTile(targetGridUid, targetGridComp, new EntityCoordinates(targetGridUid, targetLocalPos));

        return GetZTileAtPosition(targetGridUid, targetGridComp, targetIndices);
    }

    private TileAtmosphere? GetZTileAtPosition(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!TryComp<GridAtmosphereComponent>(gridUid, out var atmos))
            return null;

        if (atmos.Tiles.TryGetValue(indices, out var atmosphere))
            return atmosphere;

        return GetOrNewTile(gridUid, atmos, indices);
    }
}
