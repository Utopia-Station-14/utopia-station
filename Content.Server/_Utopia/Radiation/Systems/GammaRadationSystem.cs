using Content.Server.Radiation.Components;
using Content.Server.Radiation.Systems;
using Content.Server.Radiation.Events;
using Content.Shared.Stacks;
using Robust.Shared.Map.Components;

namespace Content.Server._Utopia.Radiation.Systems;

public sealed partial class GammaRadiationSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private RadiationSystem _radiation = default!;

    [Dependency] private EntityQuery<RadiationGridResistanceComponent> _resistanceQuery = default!;
    [Dependency] private EntityQuery<RadiationBlockingContainerComponent> _containerQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationSystemUpdatedEvent>(OnRadiationUpdated,
            before: new[] { typeof(GeigerSystem) });
    }
}
