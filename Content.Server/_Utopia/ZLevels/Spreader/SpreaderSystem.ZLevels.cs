using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Maps;
using Content.Shared.Spreader;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Content.Shared._Utopia.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server.Spreader;

public sealed partial class SpreaderSystem
{
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private void InitializeZLevels()
    {
        SubscribeLocalEvent<TileChangedEvent>(OnTileChangedZ);
    }

    private void OnTileChangedZ(ref TileChangedEvent ev)
    {
        var gridUid = ev.Entity.Owner;
        var grid = ev.Entity.Comp;

        foreach (var change in ev.Changes)
        {
            ActivateSpreadableNeighbors(gridUid, (gridUid, change.GridIndices));

            foreach (var offset in new[] { 1, -1 })
            {
                if (TryGetZTarget(gridUid, grid, change.GridIndices, offset, out var targetUid, out _, out var targetIdx))
                    ActivateSpreadableNeighbors(targetUid, (targetUid, targetIdx));
            }
        }
    }

    public bool IsHole(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, indices);
        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return true;

        return _tileDefinitionManager.TryGetDefinition(tileRef.Tile.TypeId, out var def) &&
               def is ContentTileDefinition { MapAtmosphere: true };
    }

    public bool TryGetZTarget(EntityUid gridUid, MapGridComponent grid, Vector2i indices, int offset,
        out EntityUid targetGridUid, out MapGridComponent targetGrid, out Vector2i targetIndices)
    {
        targetGridUid = default;
        targetGrid = default!;
        targetIndices = default;

        var mapUid = Transform(gridUid).MapUid;
        if (mapUid == null || !_zLevels.TryMapOffset(mapUid.Value, offset, out var targetMap))
            return false;

        var localPos = _map.GridTileToLocal(gridUid, grid, indices);
        var worldPos = _transform.ToMapCoordinates(localPos).Position;

        if (!_map.TryFindGridAt(targetMap.Owner, worldPos, out var foundUid, out var foundGrid) || foundGrid == null)
            return false;

        targetGridUid = foundUid;
        targetGrid = foundGrid;

        var targetXform = Transform(targetGridUid);
        var targetLocal = System.Numerics.Vector2.Transform(worldPos, targetXform.InvLocalMatrix);
        targetIndices = _map.LocalToTile(targetGridUid, targetGrid, new EntityCoordinates(targetGridUid, targetLocal));
        return true;
    }

    public bool TryFindDropTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices,
        out EntityUid dropGrid, out MapGridComponent dropMap, out TileRef drop)
    {
        dropGrid = default;
        dropMap = default!;
        drop = default;

        var curUid = gridUid;
        var curGrid = grid;
        var curIdx = indices;

        for (var depth = 0; depth < _cfg.GetCVar(UCCVars.UtopiaMaxZLevelsProcessUpDown); depth++)
        {
            if (!TryGetZTarget(curUid, curGrid, curIdx, -1, out var nextUid, out var nextGrid, out var nextIdx))
                return false;

            if (!IsHole(nextUid, nextGrid, nextIdx))
            {
                dropGrid = nextUid;
                dropMap = nextGrid;
                drop = _map.GetTileRef(nextUid, nextGrid, nextIdx);
                return true;
            }

            (curUid, curGrid, curIdx) = (nextUid, nextGrid, nextIdx);
        }

        return false;
    }

    public bool TryFindDropTile(EntityUid gridUid, Vector2i indices, out TileRef drop)
    {
        drop = default;
        return TryComp<MapGridComponent>(gridUid, out var grid) &&
               TryFindDropTile(gridUid, grid, indices, out _, out _, out drop);
    }
    public void GetZNeighbors(EntityUid uid,
        TransformComponent xform,
        ProtoId<EdgeSpreaderPrototype> prototype,
        ValueList<(EntityUid Grid, Vector2i Indices)> holes,
        out ValueList<(MapGridComponent, TileRef)> downFree,
        out ValueList<EntityUid> downNeighbors,
        out ValueList<(MapGridComponent, TileRef)> upFree,
        out ValueList<EntityUid> upNeighbors)
    {
        downFree = [];
        downNeighbors = [];
        upFree = [];
        upNeighbors = [];

        if (xform.GridUid is not { } gridUid ||
            xform.MapUid is not { } mapUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return;
        }

        var hasDown = _zLevels.TryMapOffset(mapUid, -1, out _);
        var hasUp = _zLevels.TryMapOffset(mapUid, 1, out _);
        if (!hasDown && !hasUp)
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var visited = new HashSet<(EntityUid, Vector2i)>();

        if (hasDown)
        {
            if (IsHole(gridUid, grid, tile) && TryFindDropTile(gridUid, grid, tile, out var dg, out var dm, out var dt))
                ClassifyZTarget(dg, dm, dt.GridIndices, prototype, visited, ref downFree, ref downNeighbors);

            foreach (var (hGrid, hIdx) in holes)
            {
                if (!TryComp<MapGridComponent>(hGrid, out var hMap))
                    continue;

                if (TryFindDropTile(hGrid, hMap, hIdx, out var dg2, out var dm2, out var dt2))
                    ClassifyZTarget(dg2, dm2, dt2.GridIndices, prototype, visited, ref downFree, ref downNeighbors);
            }
        }

        if (hasUp && TryGetZTarget(gridUid, grid, tile, 1, out var upUid, out var upGrid, out var upIdx) && IsHole(upUid, upGrid, upIdx))
        {
            for (var i = 0; i < 4; i++)
            {
                var n = upIdx.Offset((AtmosDirection) (1 << i));
                if (IsHole(upUid, upGrid, n))
                    continue;

                ClassifyZTarget(upUid, upGrid, n, prototype, visited, ref upFree, ref upNeighbors);
            }
        }
    }

    private void ClassifyZTarget(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i indices,
        ProtoId<EdgeSpreaderPrototype> prototype,
        HashSet<(EntityUid, Vector2i)> visited,
        ref ValueList<(MapGridComponent, TileRef)> free,
        ref ValueList<EntityUid> neighbors)
    {
        if (!visited.Add((gridUid, indices)))
            return;

        var tileRef = _map.GetTileRef(gridUid, grid, indices);
        if (tileRef.Tile.IsEmpty)
            return;

        var enumerator = _map.GetAnchoredEntities(gridUid, grid, indices);
        while (enumerator.MoveNext(out var ent))
        {
            if (_airtightQuery.TryGetComponent(ent, out var airtight) &&
                airtight.AirBlocked &&
                airtight.AirBlockedDirection == AtmosDirection.All &&
                !_tag.HasTag(ent.Value, IgnoredTag))
            {
                return;
            }
        }

        enumerator = _map.GetAnchoredEntities(gridUid, grid, indices);
        while (enumerator.MoveNext(out var ent))
        {
            if (_edgeSpreaderQuery.TryGetComponent(ent, out var spreader) && spreader.Id == prototype)
            {
                neighbors.Add(ent.Value);
                return;
            }
        }

        free.Add((grid, tileRef));
    }
}
