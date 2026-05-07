using System;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Log;
using Content.Shared.Maps;

namespace Content.Server._Utopia.ZLevels.Atmos;

public sealed class ZLevelAtmosTransmissionSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly CESharedZLevelsSystem _z = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float UpdateInterval = 1f;
    private const float TransferCoef = 0.08f;
    private const float MinDelta = 0.5f;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<ZLevelAtmosTransmissionComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            Process(uid);
        }
    }

    private void Process(EntityUid uid)
    {
        var xform = Transform(uid);

        if (xform.MapUid == null || xform.GridUid == null)
            return;

        var mapUid = xform.MapUid.Value;
        var gridUid = xform.GridUid.Value;

        if (!TryComp<MapGridComponent>(gridUid, out var gridComp))
            return;

        var sourceTileRef = _map.GetTileRef(gridUid, gridComp, xform.Coordinates);
        var sourceTile = sourceTileRef.GridIndices;

        var source = _atmos.GetTileMixture(gridUid, mapUid, sourceTile, true);

        if (source == null || source.TotalMoles <= 0f)
            return;

        if (_z.TryMapOffset(mapUid, 1, out var up) && up != null)
        {
            Transfer(xform.Coordinates, up.Value.Owner, source);
        }

        if (_z.TryMapOffset(mapUid, -1, out var down) && down != null)
        {
            Transfer(xform.Coordinates, down.Value.Owner, source);
        }
    }

    private EntityUid? GetGridForMap(EntityUid mapUid)
    {
        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var grid, out var xform))
        {
            if (xform.MapUid == mapUid)
                return uid;
        }
        return null;
    }

    private void Transfer(
        EntityCoordinates sourceCoords,
        EntityUid targetMapUid,
        GasMixture source)
    {
        var targetGridUid = GetGridForMap(targetMapUid);

        if (targetGridUid == null)
            return;

        if (!HasComp<MapGridComponent>(targetGridUid.Value))
            return;

        var world = _transform.ToMapCoordinates(sourceCoords);
        var targetCoords = _transform.ToCoordinates(targetGridUid.Value, world);
        var tileRef = _turf.GetTileRef(targetCoords);

        if (tileRef == null || tileRef.Value.Tile.IsEmpty)
            return;

        var target = _atmos.GetTileMixture(
            targetGridUid.Value,
            targetMapUid,
            tileRef.Value.GridIndices,
            true);

        if (target == null)
            return;

        var delta = source.Pressure - target.Pressure;
        if (delta < MinDelta)
            return;

        var moles = MathF.Min(delta * TransferCoef, source.TotalMoles * 0.25f);

        if (moles <= 0f)
            return;

        var removed = source.Remove(moles);

        foreach (var gas in Enum.GetValues<Gas>())
        {
            var amount = removed.GetMoles(gas);

            if (amount > 0f)
            {
                target.AdjustMoles(gas, amount);
            }
        }
    }
}
