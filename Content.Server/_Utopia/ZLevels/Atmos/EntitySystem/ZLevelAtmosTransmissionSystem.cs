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

namespace Content.Server._Utopia.ZLevels.Atmos;

public sealed class ZLevelAtmosTransmissionSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly CESharedZLevelsSystem _z = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

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

        Logger.Info($"[ZATMOS] {uid} SRC P={source.Pressure:0.00} M={source.TotalMoles:0.00}");
        if (_z.TryMapOffset(mapUid, 1, out var up) && up != null)
        {
            Transfer(gridUid, xform.Coordinates, up.Value.Owner, source);
        }

        if (_z.TryMapOffset(mapUid, -1, out var down) && down != null)
        {
            Transfer(gridUid, xform.Coordinates, down.Value.Owner, source);
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
        EntityUid sourceGridUid,
        EntityCoordinates sourceCoords,
        EntityUid targetMapUid,
        GasMixture source)
    {
        var targetGridUid = GetGridForMap(targetMapUid);

        if (targetGridUid == null)
        {
            Logger.Info("[ZATMOS] Grid not founded");
            return;
        }

        if (!TryComp<MapGridComponent>(targetGridUid.Value, out var targetGrid))
        {
            Logger.Info($"[ZATMOS] !MapGripComponent {targetGridUid}");
            return;
        }

        var transform = EntityManager.System<SharedTransformSystem>();
        var mapSystem = EntityManager.System<SharedMapSystem>();

        var world = transform.ToMapCoordinates(sourceCoords);
        var targetCoords = transform.ToCoordinates(targetGridUid.Value, world);
        var tileRef = mapSystem.GetTileRef(targetGridUid.Value, targetGrid, targetCoords);

        Logger.Info($"[ZATMOS] Target tile:{tileRef.GridIndices} Space?:{tileRef.Tile.IsEmpty}");

        if (tileRef.Tile.IsEmpty)
        {
            Logger.Info("[ZATMOS] Target is space");
            return;
        }

        var target = _atmos.GetTileMixture(
            targetGridUid.Value,
            targetMapUid,
            tileRef.GridIndices,
            true);

        if (target == null)
        {
            Logger.Info("[ZATMOS] Target doesn't have Mixture WHY");
            return;
        }

        Logger.Info($"[ZATMOS] Target P={target.Pressure:0.00} M={target.TotalMoles:0.00}");

        var delta = source.Pressure - target.Pressure;
        if (delta < MinDelta)
        {
            Logger.Info($"[ZATMOS] Pressure is fine");
            return;
        }

        var moles = MathF.Min(delta * TransferCoef, source.TotalMoles * 0.25f);

        Logger.Info($"[ZATMOS] Moles transfered={moles:0.00}");

        if (moles <= 0f)
            return;

        var removed = source.Remove(moles);

        Logger.Info($"[ZATMOS] Removed moles={removed.TotalMoles:0.00}");

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