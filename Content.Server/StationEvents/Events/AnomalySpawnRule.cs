using System.Linq;
using Content.Server.Anomaly;
using Content.Server.StationEvents.Components;
using Content.Server._Utopia.Supermatter.Systems;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class AnomalySpawnRule : StationEventSystem<AnomalySpawnRuleComponent>
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!; // Utopia-Tweak : ZLevels
    [Dependency] private readonly IRobustRandom _random = default!; // Utopia-Tweak : ZLevels
    // [Dependency] private SupermatterSystem _superMatter = default!; // Utopia-Tweak : Supermatter

    protected override void Added(EntityUid uid, AnomalySpawnRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryComp<StationEventComponent>(uid, out var stationEvent))
            return;

        var str = Loc.GetString("anomaly-spawn-event-announcement",
            ("sighting", Loc.GetString($"anomaly-spawn-sighting-{RobustRandom.Next(1, 6)}")));
        stationEvent.StartAnnouncement = str;

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, AnomalySpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!TryComp<StationDataComponent>(chosenStation, out var stationData))
            return;

        var grid = StationSystem.GetLargestGrid((chosenStation.Value, stationData));

        if (grid is null)
            return;

        // Utopia-Tweak : ZLevels
        var mlGrids = _zLevels.GetTargetGrids(grid.Value);
        if (mlGrids.Count == 0)
            return;

        var targetGrid = mlGrids.ElementAt(_random.Next(mlGrids.Count));

        _anomaly.SpawnOnRandomGridLocation(targetGrid, component.AnomalySpawnerPrototype);
    }
}
