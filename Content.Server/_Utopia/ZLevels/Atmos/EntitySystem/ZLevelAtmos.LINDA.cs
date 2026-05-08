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

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private bool ProcessZAtmos(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent,
            TransformComponent> ent, TileAtmosphere tile, Vector2i indices, int fireCount)
    {
        if (!IsZConnectedSpace(ent.Owner, ent.Comp3, indices))
            return false;

        ProcessZTile(ent, tile, indices, 1, fireCount);
        ProcessZTile(ent, tile, indices, -1, fireCount);

        return true;
    }

    private bool IsZConnectedSpace(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i indices)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, indices);

        if (!_turf.IsSpace(tileRef))
            return false;

        var up = GetZTile(gridUid, grid, indices, 1);
        if (up?.Air != null)
            return false;

        var down = GetZTile(gridUid, grid, indices, -1);
        if (down?.Air != null)
            return false;
        return true;
    }

    private void ProcessZTile(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent,
            MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile,
        Vector2i indices,
        int offset,
        int fireCount)
    {
        var mapUid = Transform(ent.Owner).MapUid;

        if (mapUid == null)
            return;

        if (!_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap) ||
            targetMap == null)
        {
            return;
        }

        EntityUid? targetGrid = null;
        MapGridComponent? targetGridComp = null;

        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grid, out var xform))
        {
            if (xform.MapUid == targetMap.Value.Owner)
            {
                targetGrid = uid;
                targetGridComp = grid;
                break;
            }
        }

        if (targetGrid == null || targetGridComp == null)
            return;

        var tileRef = _map.GetTileRef(targetGrid.Value, targetGridComp, indices);

        if (_turf.IsSpace(tileRef))
            return;

        var targetTile = GetZTile(ent.Owner, ent.Comp3, indices, offset);

        if (targetTile?.Air == null)
            return;

        if (fireCount > targetTile.CurrentCycle)
            Archive(targetTile, fireCount);

        AddActiveTile(ent.Comp1, targetTile);

        Share(tile, targetTile, 1);

        LastShareCheck(targetTile);
    }

    private TileAtmosphere? GetZTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset)
    {
        var mapUid = Transform(gridUid).MapUid;

        if (mapUid == null)
            return null;

        if (!_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap) ||
            targetMap == null)
        {
            return null;
        }

        EntityUid? targetGrid = null;

        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid == targetMap.Value.Owner)
            {
                targetGrid = uid;
                break;
            }
        }

        if (targetGrid == null)
            return null;

        if (!TryComp<MapGridComponent>(targetGrid.Value, out var targetGridComp))
            return null;

        var tileRef = _map.GetTileRef(targetGrid.Value, targetGridComp, indices);

        if (!TryComp<GridAtmosphereComponent>(targetGrid.Value, out var atmos))
            return null;

        if (!atmos.Tiles.TryGetValue(indices, out var atmosphere))
            return null;

        return atmosphere;
    }
}