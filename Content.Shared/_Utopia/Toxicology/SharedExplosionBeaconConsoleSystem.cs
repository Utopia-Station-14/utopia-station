using System.Diagnostics.CodeAnalysis;
using Content.Shared._Utopia.Toxicology.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Power.EntitySystems;

namespace Content.Shared._Utopia.Toxicology;

public abstract class SharedExplosionBeaconConsoleSystem : EntitySystem
{
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExplosionBeaconConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<ExplosionBeaconConsoleComponent, NewLinkEvent>(OnNewLinkConsole);
        SubscribeLocalEvent<ExplosionBeaconConsoleComponent, LinkAttemptEvent>(OnLinkAttemptConsole);
        SubscribeLocalEvent<ExplosionBeaconConsoleComponent, PortDisconnectedEvent>(OnPortDisconnectedConsole);

        SubscribeLocalEvent<ExplosionBeaconComponent, LinkAttemptEvent>(OnLinkAttemptBeacon);
    }

    private void OnConsoleMapInit(Entity<ExplosionBeaconConsoleComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent, out var source))
            return;

        foreach (var sink in _deviceLink.GetLinkedSinks((ent.Owner, source), ent.Comp.LinkingPort))
        {
            if (!TryComp<ExplosionBeaconComponent>(sink, out _))
                continue;

            ent.Comp.BeaconEntity = sink;
            Dirty(ent);

            OnBeaconLinked(ent);
            break;
        }
    }

    private void OnNewLinkConsole(Entity<ExplosionBeaconConsoleComponent> ent, ref NewLinkEvent args)
    {
        if (args.SourcePort != ent.Comp.LinkingPort || !HasComp<ExplosionBeaconComponent>(args.Sink))
            return;

        ent.Comp.BeaconEntity = args.Sink;
        Dirty(ent);

        OnBeaconLinked(ent);
    }

    private void OnLinkAttemptConsole(Entity<ExplosionBeaconConsoleComponent> ent, ref LinkAttemptEvent args)
    {
        if (ent.Comp.BeaconEntity != null)
            args.Cancel();
    }

    private void OnLinkAttemptBeacon(Entity<ExplosionBeaconComponent> ent, ref LinkAttemptEvent args)
    {
        if (!HasComp<ExplosionBeaconConsoleComponent>(args.Source))
            return;

        var query = EntityQueryEnumerator<ExplosionBeaconConsoleComponent>();
        while (query.MoveNext(out _, out var console))
        {
            if (console.BeaconEntity == ent.Owner)
            {
                args.Cancel();
                return;
            }
        }
    }

    private void OnPortDisconnectedConsole(Entity<ExplosionBeaconConsoleComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Port != ent.Comp.LinkingPort)
            return;

        ent.Comp.BeaconEntity = null;
        Dirty(ent);

        OnBeaconUnlinked(ent);
    }

    protected virtual void OnBeaconLinked(Entity<ExplosionBeaconConsoleComponent> ent) { }
    protected virtual void OnBeaconUnlinked(Entity<ExplosionBeaconConsoleComponent> ent) { }

    public bool TryGetLinkedBeacon(Entity<ExplosionBeaconConsoleComponent> console, out Entity<ExplosionBeaconComponent> beacon)
    {
        beacon = default;

        if (!TryComp<ExplosionBeaconComponent>(console.Comp.BeaconEntity, out var beaconComp))
            return false;

        if (!_power.IsPowered(console.Owner) ||
            !_power.IsPowered(console.Comp.BeaconEntity!.Value))
            return false;

        beacon = (console.Comp.BeaconEntity.Value, beaconComp);
        return true;
    }
}
