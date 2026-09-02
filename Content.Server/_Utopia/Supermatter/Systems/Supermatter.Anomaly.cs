using Content.Shared._Utopia.Supermatter.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem : EntitySystem
{
    private readonly EntProtoId EnergyAnomaly = "AnomalyElectricity";
    private readonly EntProtoId TemperatureHighAnomaly = "AnomalyPyroclastic";
    private readonly EntProtoId TemperatureLowAnomaly = "AnomalyIce";
    private readonly EntProtoId GravityAnomaly = "AnomalyGravity";
    private readonly EntProtoId BlueSpaceAnomaly = "AnomalyBluespace";

    private void ProcessAnomaly(Entity<SupermatterComponent> sm)
    {
        if (sm.Comp.Integrity > IntegrityForAnomalyLow)
            return;

        var anomalies = new List<EntProtoId>();
        GetAnomalies(sm, anomalies);
        if (anomalies.Count == 0)
            return;

        foreach (var anomaly in anomalies)
        {
            var targetCoords = GetSpawnCoords(sm);

            if (!targetCoords.IsValid(EntityManager))
                continue;

            var spawnDelay = TimeSpan.FromSeconds(AnomalyTimeBetweenSpawn);
            Timer.Spawn(spawnDelay, () =>
            {
                if (Deleted(sm))
                    return;

                var spawnedUid = Spawn(anomaly, targetCoords);
                DeleteAnomaly(spawnedUid);
            });
        }
    }

    private void DeleteAnomaly(EntityUid anomalyUid)
    {
        var lifeTime = TimeSpan.FromSeconds(_random.NextFloat(MinAnomalyTimeLife, MaxAnomalyTimeLife));

        Timer.Spawn(lifeTime, () =>
        {
            if (!Deleted(anomalyUid))
                QueueDel(anomalyUid);
        });
    }

    public void GetAnomalies(Entity<SupermatterComponent> sm, List<EntProtoId> anomalies)
    {
        if (sm.Comp.TotalEnergy > DangerAmmountEnergy)
            anomalies.Add(EnergyAnomaly);

        if (sm.Comp.TotalEnergy > ToMuchEnergy)
            anomalies.Add(BlueSpaceAnomaly);

        if (sm.Comp.CurrentTemperature > sm.Comp.MaxTemperature)
            anomalies.Add(TemperatureHighAnomaly);

        if (sm.Comp.CurrentTemperature < sm.Comp.MinTemperature)
            anomalies.Add(TemperatureLowAnomaly);
    }

    public EntityCoordinates GetSpawnCoords(Entity<SupermatterComponent> sm)
    {
        var xform = Transform(sm);
        var spawnRange = MinRangeAnomalySpawn + (sm.Comp.TotalEnergy / 1000f);
        var offsetX = _random.NextFloat(-spawnRange, spawnRange);
        var offsetY = _random.NextFloat(-spawnRange, spawnRange);

        return xform.Coordinates.Offset(new Vector2(offsetX, offsetY));
    }
}
