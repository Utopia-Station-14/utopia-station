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

public sealed class GeneratorOverheatSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GeneratorSystem _generator = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(GeneratorSystem));
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FuelGeneratorComponent, GeneratorOverheatComponent>();

        while (query.MoveNext(out var uid, out var gen, out var overheat))
        {
            if (gen.On)
                HeatGenerator(uid, gen, overheat, frameTime);

            if (overheat.CurrentTemperature >= overheat.CriticalTemperature)
                HandleCritical(uid, gen, overheat);
            else
                overheat.CriticalTriggered = false;

            CoolGenerator(uid, overheat, frameTime);

            Dirty(uid, overheat);
        }
    }

    private void HeatGenerator(EntityUid uid, FuelGeneratorComponent gen, GeneratorOverheatComponent overheat, float frameTime)
    {
        var zeroCelcius = 273.15f;
        var targetKw = gen.TargetPower / 1000f;
        var averageKw = gen.OptimalPower / 1000f;

        if (targetKw > averageKw)
        {
            var heatRate = (targetKw - averageKw) * overheat.HeatRatePerKw;

            overheat.CurrentTemperature = Math.Min(overheat.CurrentTemperature + heatRate * frameTime, overheat.CriticalTemperature);
            return;
        }

        var minKw = gen.MinTargetPower / 1000f;
        var fraction = averageKw > minKw
            ? Math.Clamp((targetKw - minKw) / (averageKw - minKw), 0f, 1f)
            : 1f;

        var equilibrium = MathHelper.Lerp(zeroCelcius, overheat.OperatingTemperature, fraction);
        var factor = 1f - MathF.Exp(-overheat.BaseHeatRate * frameTime);

        overheat.CurrentTemperature += (equilibrium - overheat.CurrentTemperature) * factor;
    }

    private void CoolGenerator(EntityUid uid, GeneratorOverheatComponent overheat, float frameTime)
    {
        var env = _atmosphere.GetContainingMixture(uid, excite: true);

        if (env == null || env.TotalMoles <= 0f)
            return;

        var dT = overheat.CurrentTemperature - env.Temperature;

        if (MathF.Abs(dT) < Atmospherics.MinimumTemperatureDeltaToConsider)
            return;

        var cGen = overheat.HeatCapacity;
        var cEnv = _atmosphere.GetHeatCapacity(env, true);

        if (cGen < Atmospherics.MinimumHeatCapacity || cEnv < Atmospherics.MinimumHeatCapacity)
            return;

        var tDivQ = (1f / cGen) + (1f / cEnv);
        var k = overheat.ThermalConductance * tDivQ;

        var dT2 = dT * MathF.Exp(-k * frameTime);
        var dE = (dT - dT2) / tDivQ;

        overheat.CurrentTemperature -= (dE / cGen) * 2;
        _atmosphere.AddHeat(env, dE);
    }

    private void HandleCritical(EntityUid uid, FuelGeneratorComponent gen, GeneratorOverheatComponent overheat)
    {
        if (overheat.CriticalTriggered)
            return;

        overheat.CriticalTriggered = true;
        _generator.SetFuelGeneratorOn(uid, false, gen);
        _popup.PopupEntity(Loc.GetString("generator-overheat-shutdown", ("generator", uid)), uid, PopupType.MediumCaution);

        if (overheat.ExplodeChance <= 1 || _random.Next(1, overheat.ExplodeChance) != overheat.ExplodeChance)
            return;

        if(TryComp<ExplosiveComponent>(uid, out var explosive))
            _explosion.TriggerExplosive(uid, explosive);
    }


    public float GetTemperatureCelsius(GeneratorOverheatComponent overheat)
        => overheat.CurrentTemperature - Atmospherics.T0C;
}