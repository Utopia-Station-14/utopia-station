using System.Numerics;
using Content.Shared_Utopia.Effects;
using Content.Shared._Utopia.Telescience.Components;
using Content.Shared._Utopia.Telescience.Messages;
using Content.Shared.Construction.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Teleportation.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Utopia.Telescience.Systems;

public sealed class TelescienceTeleporterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly LinkedEntitySystem _link = default!;
    [Dependency] private readonly SparksSystem _sparks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TelescienceTeleporterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TelescienceTeleporterComponent, TelescienceSendEvent>(OnSendEvent);
        SubscribeLocalEvent<TelescienceTeleporterComponent, TelescienceRetrieveEvent>(OnRetrieveEvent);
        SubscribeLocalEvent<TelescienceTeleporterComponent, TelescienceOpenPortalEvent>(OnOpenPortalEvent);
        SubscribeLocalEvent<TelescienceTeleporterComponent, TelescienceClosePortalEvent>(OnClosePortalEvent);
        SubscribeLocalEvent<TelescienceTeleporterComponent, RefreshPartsEvent>(OnRefreshParts);
        SubscribeLocalEvent<TelescienceTeleporterComponent, UpgradeExamineEvent>(OnUpgradeExamine);
    }

    private void OnMapInit(Entity<TelescienceTeleporterComponent> ent, ref MapInitEvent arg)
    {
        if (!TryComp<DeviceLinkSinkComponent>(ent, out var sink))
            return;

        foreach (var source in sink.LinkedSources)
        {
            if (!TryComp<TelescienceComputerComponent>(source, out var computer))
                continue;

            computer.TeleporterUid = ent;
            ent.Comp.Computer = source;
            Dirty(source, computer);
            Dirty(ent);
            break;
        }
    }

    private void OnSendEvent(Entity<TelescienceTeleporterComponent> ent, ref TelescienceSendEvent arg)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (ent.Comp.Cooldown > _time.CurTime)
            return;

        StartCooldown(ent);

        var cords = _xform.GetMapCoordinates(ent);
        var newCords = ScrambleVector(ent, arg.Coordinates);

        if (Vector2.Distance(cords.Position, newCords) > ent.Comp.TeleportMaxDistance)
            return;

        Teleport(ent, cords.Position, newCords);
        Dirty(ent);
    }

    private void OnRetrieveEvent(Entity<TelescienceTeleporterComponent> ent, ref TelescienceRetrieveEvent arg)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (ent.Comp.Cooldown > _time.CurTime)
            return;

        StartCooldown(ent);

        var coords = _xform.GetMapCoordinates(ent);
        var newCoords = ScrambleVector(ent, arg.Coordinates);

        if (Vector2.Distance(coords.Position, newCoords) > ent.Comp.TeleportMaxDistance)
            return;

        Teleport(ent, newCoords, coords.Position);
        Dirty(ent);
    }

    private void OnOpenPortalEvent(Entity<TelescienceTeleporterComponent> ent, ref TelescienceOpenPortalEvent arg)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (ent.Comp.Cooldown > _time.CurTime)
            return;

        TryNullificatePortals(ent);
        StartCooldown(ent);

        var coords = _xform.GetMapCoordinates(ent);
        var newCoords = ScrambleCoords(ent, arg.Coordinates);

        if (Vector2.Distance(coords.Position, newCoords.Position) > ent.Comp.TeleportMaxDistance)
            return;

        ent.Comp.Portals[0] = Spawn(ent.Comp.PortalEnt, coords);
        ent.Comp.Portals[1] = Spawn(ent.Comp.PortalEnt, newCoords);

        _link.TryLink(ent.Comp.Portals[0]!.Value, ent.Comp.Portals[1]!.Value, deleteOnEmptyLinks: false);
        Dirty(ent);
    }

    private void OnClosePortalEvent(Entity<TelescienceTeleporterComponent> ent, ref TelescienceClosePortalEvent arg)
    {
        if (!_power.IsPowered(ent.Owner))
            return;

        if (ent.Comp.Cooldown > _time.CurTime)
            return;

        TryNullificatePortals(ent);
        StartCooldown(ent);

        Dirty(ent);
    }

    private void Teleport(Entity<TelescienceTeleporterComponent> ent, Vector2 location, Vector2 target)
    {
        var entitiesToTeleport = _lookup.GetEntitiesInRange(
            Transform(ent).MapID,
            location,
            ent.Comp.TeleportSize,
            LookupFlags.Uncontained
        );

        if (entitiesToTeleport.Count < 1)
            return;

        var list = new List<EntityUid>();
        foreach (var n in entitiesToTeleport)
        {
            if (Transform(n).Anchored)
                continue;

            list.Add(n);
        }

        var thisOne = _random.Next(0, list.Count);
        _pullingSystem.StopAllPulls(list[thisOne]);

        _sparks.DoSparks(Transform(list[thisOne]).Coordinates);
        _xform.SetWorldPosition(list[thisOne], target);
        _sparks.DoSparks(Transform(list[thisOne]).Coordinates);
    }

    private void TryNullificatePortals(Entity<TelescienceTeleporterComponent> ent)
    {
        for (var i = 0; i < ent.Comp.Portals.Length; i++)
        {
            if (!Deleted(ent.Comp.Portals[i]))
            {
                QueueDel(ent.Comp.Portals[i]);
            }

            ent.Comp.Portals[i] = null;
        }
    }

    private Vector2 ScrambleVector(Entity<TelescienceTeleporterComponent> ent, Vector2 input)
    {
        var coords = _xform.GetMapCoordinates(ent);

        return coords.Offset(input).Position;
    }

    private MapCoordinates ScrambleCoords(Entity<TelescienceTeleporterComponent> ent, Vector2 input)
    {
        var coords = _xform.GetMapCoordinates(ent);

        return coords.Offset(input);
    }

    private void StartCooldown(Entity<TelescienceTeleporterComponent> ent)
    {
        ent.Comp.Cooldown = _time.CurTime + ent.Comp.CooldownInterval;

        if (ent.Comp.Computer == null)
            return;

        Dirty(ent);
        RaiseLocalEvent(ent.Comp.Computer.Value, new TelescienceCooldownEvent(ent.Comp.Cooldown));
    }

    private void OnRefreshParts(Entity<TelescienceTeleporterComponent> ent, ref RefreshPartsEvent args)
    {
        var manipTier = args.PartTiers[ent.Comp.MachinePartAddDistance];

        ent.Comp.TeleportMaxDistance =
            ent.Comp.BaseTeleportMaxDistance * MathF.Pow(ent.Comp.PartTierAddDistanceMultiplier, manipTier - 1);
    }

    private void OnUpgradeExamine(Entity<TelescienceTeleporterComponent> ent, ref UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("telepad-max-distance-upgrade", ent.Comp.TeleportMaxDistance / ent.Comp.BaseTeleportMaxDistance);
    }
}
