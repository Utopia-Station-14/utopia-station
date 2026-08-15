using Content.Shared._Utopia.ZLevels.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Utopia.ZLevels.Systems;

public readonly record struct GridCoverage(HashSet<EntityUid> GridUids, MapId FallbackMapId, bool HasGrid);

public sealed class SharedGridCoverageSystem : EntitySystem
{
    [Dependency] private readonly SharedGridMotionLinkSystem _motionLinkSystem = default!;

    public HashSet<EntityUid> GetLinkedGrids(EntityUid gridUid)
    {
        var grids = new HashSet<EntityUid> { gridUid };

        if (!TryComp<GridMotionLinkComponent>(gridUid, out var linked))
            return grids;

        foreach (var peerGrid in _motionLinkSystem.GetGridsOfGroup(linked.GroupId))
        {
            grids.Add(peerGrid);
        }

        return grids;
    }

    public GridCoverage GetGridCoverage(EntityUid source, TransformComponent? xform = null)
    {
        xform ??= Transform(source);

        if (TryGetEffectiveGrid(source, xform, out var gridUid))
            return new GridCoverage(GetLinkedGrids(gridUid), xform.MapID, true);

        return new GridCoverage([], xform.MapID, false);
    }

    public bool IsInCoverage(GridCoverage coverage, EntityUid target, TransformComponent? targetXform = null)
    {
        targetXform ??= Transform(target);

        if (coverage.HasGrid)
            return TryGetEffectiveGrid(target, targetXform, out var gridUid) && coverage.GridUids.Contains(gridUid);

        return targetXform.MapID == coverage.FallbackMapId;
    }


    private bool TryGetEffectiveGrid(EntityUid uid, TransformComponent xform, out EntityUid gridUid)
    {
        if (xform.GridUid is { } directGrid)
        {
            gridUid = directGrid;
            return true;
        }

        if (HasComp<MapGridComponent>(uid))
        {
            gridUid = uid;
            return true;
        }

        var parent = xform.ParentUid;
        var remaining = 16;
        while (parent.IsValid() && remaining-- > 0)
        {
            if (HasComp<MapGridComponent>(parent))
            {
                gridUid = parent;
                return true;
            }

            if (!TryComp(parent, out TransformComponent? parentXform))
                break;

            if (parentXform.GridUid is { } parentGrid)
            {
                gridUid = parentGrid;
                return true;
            }

            if (parent == parentXform.ParentUid)
                break;

            parent = parentXform.ParentUid;
        }

        gridUid = EntityUid.Invalid;
        return false;
    }
}
