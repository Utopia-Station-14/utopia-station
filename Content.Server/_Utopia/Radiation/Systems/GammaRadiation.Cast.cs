using System.Numerics;
using Content.Server.Radiation.Components;
using Content.Server.Radiation.Events;
using Content.Server.Radiation.Systems;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Utopia.Radiation.Systems;
public sealed partial class GammaRadiationSystem
{
    private readonly record struct GammaSourceData(float Intensity, RadiationSourceComponent Source, TransformComponent Xform, Vector2 WorldPosition);

    private const float GammaPenetration = 0.35f;
    private const float GammaScatterFraction = 0.02f;

    private readonly List<GammaSourceData> _sources = new();
    private readonly List<(EntityUid Uid, float Rads)> _pending = new();
    private List<Entity<MapGridComponent>> _grids = new();

    private void OnRadiationUpdated(RadiationSystemUpdatedEvent ev)
    {
        _sources.Clear();
        _pending.Clear();

        var sources = EntityQueryEnumerator<RadiationSourceComponent, TransformComponent>();
        while (sources.MoveNext(out var uid, out var source, out var xform))
        {
            if (!source.Enabled || source.GammaIntensity <= 0)
                continue;

            var intensity = source.GammaIntensity * _stack.GetCount(uid);
            intensity = GetContainerAdjusted(uid, intensity);
            if (intensity <= 0)
                continue;

            _sources.Add(new GammaSourceData(intensity, source, xform, _transform.GetWorldPosition(xform)));
        }

        if (_sources.Count == 0)
            return;

        var receivers = EntityQueryEnumerator<RadiationReceiverComponent, TransformComponent>();
        while (receivers.MoveNext(out var uid, out var receiver, out var xform))
        {
            var destWorld = _transform.GetWorldPosition(xform);

            var rads = 0f;
            foreach (var source in _sources)
                rads += Cast(source, xform, destWorld);

            rads = GetContainerAdjusted(uid, rads);
            if (rads <= 0)
                continue;

            receiver.GammaRadiation = rads;
            _pending.Add((uid, rads));
        }

        foreach (var (uid, rads) in _pending)
            _radiation.IrradiateEntity(uid, rads, _radiation.GridcastUpdateRate);
    }

    private float Cast(in GammaSourceData src, TransformComponent destXform, Vector2 destWorld)
    {
        if (src.Xform.MapID != destXform.MapID)
            return 0f;

        var dist = Math.Max((destWorld - src.WorldPosition).Length(), 0.5f);
        var rads = src.Intensity / dist;
        if (dist > src.Source.GammaTerminalDecayDistance)
            rads -= src.Source.GammaTerminalDecaySlope * (dist - src.Source.GammaTerminalDecayDistance);

        if (rads < 0.01f)
            return 0f;

        var mapId = destXform.MapID;

        if (_radiation.GridcastSimplifiedSameGrid
            && destXform.GridUid is {} sameGrid
            && src.Xform.GridUid == sameGrid)
        {
            return _gridQuery.TryComp(sameGrid, out var gridComp)
                ? Gridcast(sameGrid, gridComp, rads, src, destXform, destWorld)
                : rads;
        }

        var box = Box2.FromTwoPoints(src.WorldPosition, destWorld);
        _grids.Clear();
        _maps.FindGridsIntersecting(mapId, box, ref _grids, true);

        foreach (var grid in _grids)
        {
            rads = Gridcast(grid.Owner, grid.Comp, rads, src, destXform, destWorld);
            if (rads <= 0)
                break;
        }

        _grids.Clear();
        return rads;
    }

    private float Gridcast(EntityUid gridUid,MapGridComponent grid, float rads, in GammaSourceData src,TransformComponent destXform, Vector2 destWorld)
    {
        if (!_resistanceQuery.TryComp(gridUid, out var resistance))
            return rads;

        var map = resistance.GammaResistancePerTile;
        if (map.Count == 0)
            return rads;

        var gridXform = Transform(gridUid);

        var srcLocal = src.Xform.ParentUid == gridUid
            ? src.Xform.LocalPosition
            : Vector2.Transform(src.WorldPosition, gridXform.InvLocalMatrix);

        var dstLocal = destXform.ParentUid == gridUid
            ? destXform.LocalPosition
            : Vector2.Transform(destWorld, gridXform.InvLocalMatrix);

        var srcGrid = srcLocal / grid.TileSize;
        var dstGrid = dstLocal / grid.TileSize;
        var scatterFloor = rads * GammaScatterFraction;

        foreach (var (tile, distInCell) in RadiationSystem.AdvancedGridRaycast(srcGrid, dstGrid))
        {
            if (!map.TryGetValue(tile, out var res))
                continue;

            rads *= MathF.Exp(-GammaPenetration * res * distInCell);
            if (rads < scatterFloor)
                rads = scatterFloor;

            if (rads <= _radiation.MinIntensity)
                return 0f;
        }

        return rads;
    }

    private float GetContainerAdjusted(EntityUid uid, float rads)
    {
        var child = uid;
        var parent = Transform(uid).ParentUid;

        while (parent.IsValid())
        {
            if ((MetaData(child).Flags & MetaDataFlags.InContainer) != 0
                && _containerQuery.TryComp(parent, out var container))
            {
                rads = MathF.Max(rads - container.GammaResistance, 0f) * container.GammaMultiplier;
                if (rads < 0.1f)
                    return 0f;
            }

            child = parent;
            parent = Transform(parent).ParentUid;
        }

        return rads;
    }
}
