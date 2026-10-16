using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Server.Power.Generator;
using Content.Server.Power.Components;
using Content.Shared.Power.Generator;
using Content.Shared.Atmos;
using Content.Shared.Explosion.Components;
using Content.Shared.Popups;
using Content.Shared._Utopia.Power.Generator;
using Robust.Shared.Random;
using Robust.Shared.Maths;

namespace Content.Server._Utopia.Power.Generator;

public sealed partial class GeneratorOverheatSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private GeneratorSystem _generator = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    private const float UpdateInterval = 1f;
    private float _accumulator;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(GeneratorSystem));
    }

    public override void Update(float frameTime)
    {
        _accumulator += frameTime;

        if (_accumulator < UpdateInterval)
            return;

        while (_accumulator >= UpdateInterval)
        {
            _accumulator -= UpdateInterval;

            var query = EntityQueryEnumerator<FuelGeneratorComponent, GeneratorOverheatComponent>();

            while (query.MoveNext(out var uid, out var gen, out var overheat))
            {
                if (gen.On)
                    HeatGenerator(uid, gen, overheat);

                if (overheat.CurrentTemperature >= overheat.CriticalTemperature)
                    HandleCritical(uid, gen, overheat);
                else
                    overheat.CriticalTriggered = false;

                CoolGenerator(uid, overheat);

                Dirty(uid, overheat);
            }
        }
    }

    private void HeatGenerator(EntityUid uid, FuelGeneratorComponent gen, GeneratorOverheatComponent overheat)
    {
        var dt = UpdateInterval;

        var targetKw = gen.TargetPower / 1000f;
        var averageKw = gen.OptimalPower / 1000f;

        if (targetKw > averageKw)
        {
            var heatRate = (targetKw - averageKw) * overheat.HeatRatePerKw;

            overheat.CurrentTemperature = Math.Min(
                overheat.CurrentTemperature + heatRate * dt,
                overheat.CriticalTemperature);

            return;
        }

        var minKw = gen.MinTargetPower / 1000f;
        var fraction = averageKw > minKw
            ? Math.Clamp((targetKw - minKw) / (averageKw - minKw), 0f, 1f)
            : 1f;

        var equilibrium = MathHelper.Lerp(Atmospherics.T0C, overheat.OperatingTemperature, fraction);
        var factor = 1f - MathF.Exp(-overheat.BaseHeatRate * dt);

        overheat.CurrentTemperature += (equilibrium - overheat.CurrentTemperature) * factor;
    }

    private void CoolGenerator(EntityUid uid, GeneratorOverheatComponent overheat)
    {
        var env = _atmosphere.GetContainingMixture(uid, excite: true);

        if (env == null || env.TotalMoles <= 0f)
        {
            CoolInSpace(uid, overheat);
            return;
        }

        var dT = overheat.CurrentTemperature - env.Temperature;

        if (MathF.Abs(dT) < Atmospherics.MinimumTemperatureDeltaToConsider)
            return;

        var cGen = overheat.HeatCapacity;
        var cEnv = _atmosphere.GetHeatCapacity(env, true);

        if (cGen < Atmospherics.MinimumHeatCapacity || cEnv < Atmospherics.MinimumHeatCapacity)
            return;

        var dt = UpdateInterval;

        var tDivQ = (1f / cGen) + (1f / cEnv);
        var k = overheat.ThermalConductance * tDivQ;

        var dT2 = dT * MathF.Exp(-k * dt);
        var dE = (dT - dT2) / tDivQ;

        overheat.CurrentTemperature -= (dE / cGen) * 2f;
        _atmosphere.AddHeat(env, dE);
    }

    private void CoolInSpace(EntityUid uid, GeneratorOverheatComponent overheat)
    {
        var dt = UpdateInterval;
        var coolingRate = (overheat.BaseHeatRate / 4);

        var delta = overheat.CurrentTemperature - Atmospherics.T0C;

        if (delta <= 0f)
            return;

        var factor = MathF.Exp(-coolingRate * dt);
        overheat.CurrentTemperature -= factor;
    }

    private void HandleCritical(EntityUid uid, FuelGeneratorComponent gen, GeneratorOverheatComponent overheat)
    {
        if (overheat.CriticalTriggered)
            return;

        overheat.CriticalTriggered = true;

        _generator.SetFuelGeneratorOn(uid, false, gen);

        _popup.PopupEntity(
            Loc.GetString("generator-overheat-shutdown", ("generator", uid)),
            uid,
            PopupType.MediumCaution);

        if (overheat.ExplodeChance <= 1 ||
            _random.Next(1, overheat.ExplodeChance) != overheat.ExplodeChance)
            return;

        if (TryComp<ExplosiveComponent>(uid, out var explosive))
            _explosion.TriggerExplosive(uid, explosive);
    }

    public float GetTemperatureCelsius(GeneratorOverheatComponent overheat)
        => overheat.CurrentTemperature - Atmospherics.T0C;
}
