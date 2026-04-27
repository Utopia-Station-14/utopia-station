using Content.Server.Atmos.EntitySystems;
using Content.Server._Utopia.ZLevels.Transmission.Systems;
using Content.Shared._Utopia.ZLevels.Transmission.Components;
using Content.Shared.Atmos;

namespace Content.Server._Utopia.ZLevels.Atmos;

/// <summary>
/// Передает газ между одинаковыми координатами на соседних Z-уровнях.
/// Если давление у исходного тайла выше - половина смеси уходит к соседу в гости.
/// </summary>
public sealed class ZLevelAtmosTransmissionSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly ZLevelTransmissionSystem _zTransmission = default!;

    private float _accumulator;
    private const float UpdateInterval = 1.0f;

    private readonly Dictionary<EntityUid, EntityUid> _links = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelAtmosTransmissionComponent, ComponentStartup>(OnRefresh);
        SubscribeLocalEvent<ZLevelAtmosTransmissionComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<ZLevelAtmosTransmissionComponent, ComponentShutdown>(OnShutdown);
    }

    // TODO: Заменить на AtmosDeviceUpdate
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator -= UpdateInterval;

        var query = EntityQueryEnumerator<ZLevelAtmosTransmissionComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_links.TryGetValue(uid, out var target))
                Transfer(uid, target);
        }
    }

    private void OnRefresh(EntityUid uid, ZLevelAtmosTransmissionComponent comp, ComponentStartup args)
        => Refresh(uid);

    private void OnMove(EntityUid uid, ZLevelAtmosTransmissionComponent comp, ref MoveEvent args)
        => Refresh(uid);

    private void OnShutdown(EntityUid uid, ZLevelAtmosTransmissionComponent comp, ComponentShutdown args)
        => _links.Remove(uid);

    private void Refresh(EntityUid uid)
    {
        if (!TryComp(uid, out ZLevelEntityLinkComponent? link))
            return;

        EntityUid? target = null;

        if (link.AboveMap is { } above)
            target = _zTransmission.TryFindAtmosTarget(uid, above);

        if (target == null && link.BelowMap is { } below)
            target = _zTransmission.TryFindAtmosTarget(uid, below);

        if (target != null)
            _links[uid] = target.Value;
        else
            _links.Remove(uid);
    }

    // TODO: БОЛЬШЕ ФИЗИКИ СУКОО
    private void Transfer(EntityUid fromUid, EntityUid toUid)
    {
        var fromMix = _atmosphere.GetContainingMixture(fromUid, false, true);
        var toMix = _atmosphere.GetContainingMixture(toUid, false, true);

        if (fromMix == null || toMix == null)
            return;

        if (fromMix.Pressure <= toMix.Pressure)
            return;

        var removed = fromMix.RemoveRatio(0.5f);

        if (removed.TotalMoles <= 0f)
            return;

        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            var amount = removed.GetMoles(i);

            if (amount > 0f)
                toMix.AdjustMoles(i, amount);
        }

        toMix.Temperature = (toMix.Temperature + removed.Temperature) * 0.5f;
    }
}