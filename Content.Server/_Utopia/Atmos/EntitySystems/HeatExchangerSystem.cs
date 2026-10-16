using Content.Server._Utopia.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.NodeContainer.Nodes;
using Content.Server.NodeContainer.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._Utopia.Atmos.EntitySystems;

public sealed partial class HeatExchangingPipeSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private NodeContainerSystem _nodes = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HeatExchangingPipeComponent, AtmosDeviceUpdateEvent>(OnAtmosUpdate);
    }

    /// <summary>
    /// Система радиаторов. Основное отличие от системы визардов - небольшое упрощение и отсутсвие манипуляций с давлением, из-за чего ставить радиаторы последовательно было очень больно.
    /// В данном методе идут базовые проверки на наличие газа в трубе/на тайле, после чего используется необходимый тип охлаждения.
    /// </summary>
    private void OnAtmosUpdate(EntityUid uid, HeatExchangingPipeComponent comp, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodes.TryGetNode(uid, comp.NodeName, out PipeNode? pipe))
            return;

        // Если на тайле радиатора стоит сущность - скип.
        if (args.Grid is { } grid
            && _transform.TryGetGridTilePosition(uid, out var tile)
            && _atmos.IsTileAirBlockedCached(grid, tile))
        {
            return;
        }

        // Здесь всё начинается. Ну или заканчивается, если газа в трубе нету.
        var air = pipe.Air;
        if (air.TotalMoles <= 0f)
            return;

        var env = _atmos.GetContainingMixture(uid, true, true);

        // Если вне трубы вакуум - вызывается отдельный тип охлаждения. Иначе идёт базовый теплообмен между тайлом и трубой.
        if (env == null || env.TotalMoles <= 0f)
        {
            HeatExchangeInVacuum(air, comp, args.dt);
            HandleBuckledMobs(uid, air.Temperature, args.dt, comp);
            return;
        }

        HeatExchange(air, env, comp, args.dt);
        HandleBuckledMobs(uid, air.Temperature, args.dt, comp);
    }

    /// <summary>
    /// Метод отвечающий за базовую теплопередачу газов между тайлом и трубой.
    /// </summary>
    private void HeatExchange(GasMixture air, GasMixture env, HeatExchangingPipeComponent comp, float dt)
    {
        // dT является разницей температур между трубой и тайлом.
        // Если разница достаточно низкая, можно проигнорировать передачу.
        var dT = air.Temperature - env.Temperature;
        if (MathF.Abs(dT) < comp.MinimumTemperatureDifference)
            return;

        // Собираем теплоемкость газов.
        var cPipe = _atmos.GetHeatCapacity(air, true);
        var cEnv = _atmos.GetHeatCapacity(env, true);

        if (cPipe < Atmospherics.MinimumHeatCapacity ||
            cEnv < Atmospherics.MinimumHeatCapacity)
            return;

        // TdivQ - коэффициент перевода энергии в изменение температуры
        // Используется для симметричного обмена энергией
        var tDivQ = 1f / cPipe + 1f / cEnv;

        // Немного конвекции ниже.
        var k = comp.ThermalConductivity * tDivQ;
        var dT2 = dT * MathF.Exp(-k * dt);

        // Количество переданной энергии
        // Если dE положительное - тепло уходит из трубы на тайл.
        // Если dE отрицательное - тепло уходит из тайла в трубу.
        var dE = (dT - dT2) / tDivQ;

        // И завершаем всё передачей тепла.
        _atmos.AddHeat(air, -dE);
        _atmos.AddHeat(env, dE);
    }

    /// <summary>
    /// Упрощенный метод передачи тепла с трубы, когда на тайле вакуум.
    /// </summary>
    private void HeatExchangeInVacuum(GasMixture air, HeatExchangingPipeComponent comp, float dt)
    {
        // Берём теплоемкость газа с трубы.
        var cPipe = _atmos.GetHeatCapacity(air, true);
        if (cPipe < Atmospherics.MinimumHeatCapacity)
            return;

        // Разница температуры между трубой и в данном случае вакуумом.
        var dT = air.Temperature - Atmospherics.TCMB;

        // Немного конвекции ниже.
        var k = comp.RadiationCoefficient / cPipe;
        var dT2 = dT * MathF.Exp(-k * dt);

        // Завершаем всё передачей тепла в вакуум.
        var dE = (dT - dT2) / (1f / cPipe);
        _atmos.AddHeat(air, -dE);
    }

    /// <summary>
    /// Наносим урон сущности... которая пристёгнута к РАДИАТОРУ.
    /// </summary>
    private void HandleBuckledMobs(
        EntityUid uid,
        float temperature,
        float dt,
        HeatExchangingPipeComponent comp)
    {
        if (!TryComp<StrapComponent>(uid, out var strap))
            return;

        if (temperature <= comp.BurnStart)
            return;

        foreach (var buckled in strap.BuckledEntities)
        {
            if (!TryComp<BuckleComponent>(buckled, out var buckle))
                continue;

            if (buckle.BuckledTo != uid)
                continue;

            var damageSpecifier = new DamageSpecifier();

            var damageAmount = FixedPoint2.New(
                (temperature - comp.BurnStart) * dt * 0.01f
            );

            damageSpecifier.DamageDict.Add("Burn", damageAmount);

            _damage.ChangeDamage(buckled, damageSpecifier, true);
        }
    }
}
