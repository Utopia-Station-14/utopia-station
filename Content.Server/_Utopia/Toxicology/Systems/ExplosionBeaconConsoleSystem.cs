using Content.Shared._Utopia.Toxicology;
using Content.Shared._Utopia.Toxicology.Components;
using Content.Shared.DeviceLinking.Events;
using Robust.Server.GameObjects;

namespace Content.Server._Utopia.Toxicology;

public sealed class ExplosionBeaconConsoleSystem : SharedExplosionBeaconConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosionBeaconConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ExplosionBeaconConsoleComponent, AfterAutoHandleStateEvent>(OnConsoleState);
    }

    private void OnUiOpened(Entity<ExplosionBeaconConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    private void OnConsoleState(Entity<ExplosionBeaconConsoleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUserInterface(ent);
    }

    public void UpdateConsolesForBeacon(Entity<ExplosionBeaconComponent> beacon)
    {
        var query = EntityQueryEnumerator<ExplosionBeaconConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.BeaconEntity == beacon.Owner)
                UpdateUserInterface((uid, console));
        }
    }

    public void UpdateUserInterface(Entity<ExplosionBeaconConsoleComponent> console)
    {
        if (!TryGetLinkedBeacon(console, out var beacon))
        {
            _ui.SetUiState(
                console.Owner,
                ExplosionBeaconConsoleUiKey.Key,
                ExplosionBeaconConsoleState.Unlinked);
            return;
        }

        var state = new ExplosionBeaconConsoleState(
            linked: true,
            targetIntensity: beacon.Comp.TargetIntensity,
            targetCurrentIntensity: beacon.Comp.TargetCurrentIntensity,
            currentAttempt: beacon.Comp.CurrentAttempt,
            maxAttempts: beacon.Comp.MaxAttempts,
            lastTotalIntensity: beacon.Comp.LastTotalIntensity,
            lastCurrentIntensity: beacon.Comp.LastCurrentIntensity,
            lastPoints: beacon.Comp.LastPoints);

        _ui.SetUiState(
            console.Owner,
            ExplosionBeaconConsoleUiKey.Key,
            state);
    }

    protected override void OnBeaconLinked(Entity<ExplosionBeaconConsoleComponent> ent)
    {
        UpdateUserInterface(ent);
    }

    protected override void OnBeaconUnlinked(Entity<ExplosionBeaconConsoleComponent> ent)
    {
        UpdateUserInterface(ent);
    }
}
