using Content.Shared._Utopia.Explosion.Events;
using Content.Shared._Utopia.Toxicology.Components;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Toxicology.Systems;

/// <summary>
/// Система маяка, который отслеживает параметры взрывов.
/// </summary>
public sealed class ExplosionBeaconSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ExplosionBeaconConsoleSystem _console = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosionBeaconComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ExplosionBeaconComponent, ExplosionPowerEvent>(OnExplosionHit);
    }

    private void OnMapInit(Entity<ExplosionBeaconComponent> ent, ref MapInitEvent args)
    {
        RandomTargetNumbers(ent);
    }

    private void OnExplosionHit(Entity<ExplosionBeaconComponent> beacon, ref ExplosionPowerEvent args)
    {
        Process(beacon, args.Slope, args.TotalIntensity, args.CurrentIntensity);
        _console.UpdateConsolesForBeacon(beacon);
    }

    private void Process(Entity<ExplosionBeaconComponent> beacon, float slope, float totalIntensity, float currentIntensity)
    {
        var slopePoints = GetPoints(slope, beacon.Comp.TargetSlope);
        var intensityPoints = GetPoints(totalIntensity, beacon.Comp.TargetIntensity);
        var currentPoints = GetPoints(currentIntensity, beacon.Comp.TargetCurrentIntensity);

        var points = slopePoints + intensityPoints + currentPoints;

        beacon.Comp.LastSlope = slope;
        beacon.Comp.LastTotalIntensity = totalIntensity;
        beacon.Comp.LastCurrentIntensity = currentIntensity;
        beacon.Comp.LastPoints = points;

        TransferPoints(beacon, points);
    }

    private int GetPoints(float value, float target)
    {
        if (value <= 0 || target <= 0)
            return 0;

        var ratio = MathF.Min(value, target) / MathF.Max(value, target);
        ratio = MathF.Max(ratio, 0.1f) * 10;

        return (int) ratio;
    }

    private void TransferPoints(Entity<ExplosionBeaconComponent> beacon, int points)
    {
        if (beacon.Comp.CurrentAttempt > beacon.Comp.MaxAttempts)
        {
            RandomTargetNumbers(beacon);
            beacon.Comp.CurrentAttempt = 0;
            _console.UpdateConsolesForBeacon(beacon);
            return;
        }

        if (points < beacon.Comp.MinPoints)
            beacon.Comp.CurrentAttempt += 1;

        var multiplier = 1f;
        switch (beacon.Comp.CurrentAttempt)
        {
            case 0:
                multiplier += 2f;
                break;
            case 1:
                multiplier += 1.5f;
                break;
        }

        points = (int) (points * multiplier);
        beacon.Comp.LastPoints = points;
        // TODO: передача очков на сервер РнД
    }

    public void RandomTargetNumbers(Entity<ExplosionBeaconComponent> beacon)
    {
        beacon.Comp.TargetSlope = _random.Next(beacon.Comp.TargetSlopeMin, beacon.Comp.TargetSlopeMax);
        beacon.Comp.TargetIntensity = _random.Next(beacon.Comp.TargetIntensityMin, beacon.Comp.TargetIntensityMax);
        beacon.Comp.TargetCurrentIntensity = _random.Next(beacon.Comp.TargetIntensityMin, beacon.Comp.TargetIntensityMax);
    }
}
