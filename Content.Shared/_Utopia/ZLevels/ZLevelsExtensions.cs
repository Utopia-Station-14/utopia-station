using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map;

namespace Content.Shared._Utopia.ZLevels;

public sealed partial class ZLevelsExtensions : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public bool InRange(Entity<TransformComponent?> entA, Entity<TransformComponent?> entB, float range)
    {
        if (!Resolve(entA, ref entA.Comp, logMissing: false))
            return false;

        if (!Resolve(entB, ref entB.Comp, logMissing: false))
            return false;

        var rangeSquared = range * range;

        if (!entA.Comp.ParentUid.IsValid() || !entB.Comp.ParentUid.IsValid())
            return false;

        if (entA.Comp.ParentUid == entB.Comp.ParentUid)
            return (entA.Comp.LocalPosition - entB.Comp.LocalPosition).LengthSquared() < rangeSquared;

        if (entA.Comp.ParentUid == entB.Owner)
            return entA.Comp.LocalPosition.LengthSquared() < rangeSquared;

        if (entB.Comp.ParentUid == entB.Owner)
            return entB.Comp.LocalPosition.LengthSquared() < rangeSquared;

        if (entA.Comp.MapID == MapId.Nullspace || entB.Comp.MapID == MapId.Nullspace)
            return false;

        if (entA.Comp.MapID != entB.Comp.MapID && !IsSameZLevel(entA, entB))
            return false;

        var coordA = _transform.GetMapCoordinates(entA.Owner);
        var coordB = _transform.GetMapCoordinates(entB.Owner);
        return (coordA.Position - coordB.Position).LengthSquared() < rangeSquared;
    }

    public bool IsSameZLevel(Entity<TransformComponent?> entA, Entity<TransformComponent?> entB)
    {
        if (!Resolve(entA, ref entA.Comp, logMissing: false))
            return false;

        if (!Resolve(entB, ref entB.Comp, logMissing: false))
            return false;

        if (entA.Comp.MapID == entB.Comp.MapID)
            return true;

        if (entA.Comp.MapUid is not { } mapA || entB.Comp.MapUid is not { } mapB)
            return false;

        if (!TryComp<CEZMapComponent>(mapA, out var aZMap) || !TryComp<CEZMapComponent>(mapB, out var bZMap))
            return false;

        return aZMap.NetworkUid == bZMap.NetworkUid;
    }

    public bool IsSameZNetwork(EntityUid? mapA, EntityUid? mapB)
    {
        if (mapA is not { } a || mapB is not { } b)
            return false;

        if (!TryComp<CEZMapComponent>(a, out var aZMap) || !TryComp<CEZMapComponent>(b, out var bZMap))
            return false;

        return aZMap.NetworkUid == bZMap.NetworkUid;
    }
}
