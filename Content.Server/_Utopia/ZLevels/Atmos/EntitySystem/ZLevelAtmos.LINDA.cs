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

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private void InitializeZAtmos()
    {
    }

    /// <summary>
    /// AtmosphereSystem.LINDA метод ProcessCell.
    /// </summary>
    private void ShareZLevelAtmos(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile, int fireCount)
    {
        var indices = tile.GridIndices;
        if (!IsZConnectedSpace(ent.Owner, ent.Comp3, indices))
            return;

        ProcessZTile(ent, tile, indices, 1, fireCount);
        ProcessZTile(ent, tile, indices, -1, fireCount);
    }

    /// <summary>
    /// AtmosphereSystem.LINDA метод ProcessRevalidate.
    /// </summary>
    private void RefreshZAtmosTransferCandidates(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent, 
        Vector2i indices)
    {
        // При необходимости обновляет кэш или связи для измененного тайла
    }

    /// <summary>
    /// AtmosphereSystem.Processing ProcessRevalidate.
    /// </summary>
    private void ActivateZAtmosTransferCandidate(GridAtmosphereComponent atmos, TileAtmosphere tile)
    {
        // Обновление сетки атмос-тайлов.
    }

    /// <summary>
    /// AtmosphereSystem.Processing метод UpdateProcessing.
    /// </summary>
    private void RunZAtmosProcessing()
    {
        // Вызывается в начале каждого тика симуляции атмосферы
    }

    /// <summary>
    /// AtmosphereSystem.Grid метод GetContainingMixture.
    /// </summary>
    private bool ShouldTryZLevelProtectedMixture(Entity<TransformComponent?> ent, EntityUid? gridUid, Vector2i position)
    {
        if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var gridComp))
            return false;

        return IsZConnectedSpace(gridUid.Value, gridComp, position);
    }

    /// <summary>
    /// AtmosphereSystem.Grid.cs метод GetContainingMixture.
    /// </summary>
    private bool TryGetZLevelProtectedTileMixtureForEntity(Entity<TransformComponent?> ent, bool excite, [NotNullWhen(true)] out GasMixture? mixture)
    {
        mixture = null;
        
        if (ent.Comp == null || ent.Comp.GridUid == null || !TryComp<MapGridComponent>(ent.Comp.GridUid.Value, out var gridComp))
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
        if (down != null && !down.Space)
            return true;

        var up = GetZTile(gridUid, grid, indices, 1);
        if (up != null && !up.Space)
            return true;

        return false;
    }

    /// <summary>
    /// AtmosphereSystem.Processing метод UpdateTileData.
    /// </summary>
    private bool HasZLevelTileBelow(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,  TransformComponent> ent, Vector2i indices)
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

    private bool IsZConnectedSpace(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        if (!TryComp<GridAtmosphereComponent>(gridUid, out var atmos))
            return false;

        if (!atmos.Tiles.TryGetValue(indices, out var tile))
            return false;

        if (!tile.Space && !tile.MapAtmosphere && !tile.NoGridTile)
            return false;

        var up = GetZTile(gridUid, grid, indices, 1);
        if (up?.Air != null)
            return true;

        var down = GetZTile(gridUid, grid, indices, -1);
        if (down?.Air != null)
            return true;

        return false;
    }

    private void ProcessZTile(Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile, Vector2i indices, int offset, int fireCount)
    {
        var mapUid = ent.Comp4.MapUid;
        if (mapUid == null)
            return;

        if (!_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap) || targetMap == null)
            return;

        // Немного работы с координатами.
        var localPos = _mapSystem.GridTileToLocal(ent.Owner, ent.Comp3, indices);
        var worldPos = _transformSystem.GetWorldPosition(localPos.EntityId);

        if (!_mapManager.TryFindGridAt(targetMap.Value.Owner, worldPos, out var targetGridUid, out var targetGridComp))
            return;

        var targetGridXform = Comp<TransformComponent>(targetGridUid);
        var targetLocalPos = Vector2.Transform(worldPos, targetGridXform.InvLocalMatrix);
        var targetIndices = _mapSystem.LocalToTile(targetGridUid, targetGridComp, new EntityCoordinates(targetGridUid, targetLocalPos));

        var targetTile = GetZTileAtPosition(targetGridUid, targetGridComp, targetIndices);
        // Конец небольшой работы с координатами.
        
        if (targetTile?.Air == null || targetTile.Space || targetTile.MapAtmosphere)
            return;

        if (fireCount > targetTile.CurrentCycle)
            Archive(targetTile, fireCount);

        if (TryComp<GridAtmosphereComponent>(targetGridUid, out var targetGridAtmos))
            AddActiveTile(targetGridAtmos, targetTile);

        Share(tile, targetTile, 1);
        LastShareCheck(targetTile);
    }

    private TileAtmosphere? GetZTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset)
    {
        var mapUid = Transform(gridUid).MapUid;
        if (mapUid == null)
            return null;

        if (!_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap) || targetMap == null)
            return null;

        var localPos = _mapSystem.GridTileToLocal(gridUid, grid, indices);
        var worldPos = _transformSystem.GetWorldPosition(localPos.EntityId);

        if (!_mapManager.TryFindGridAt(targetMap.Value.Owner, worldPos, out var targetGridUid, out var targetGridComp))
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

        if (!atmos.Tiles.TryGetValue(indices, out var atmosphere))
            return null;

        return atmosphere;
    }
}