using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Magic.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationFirebreathSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private const string Action = "ActionGeneticFireball";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MutationFirebreathComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MutationFirebreathComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MutationFirebreathComponent, ProjectileSpellEvent>(OnFireball);
    }

    private void OnInit(EntityUid uid, MutationFirebreathComponent comp, ComponentInit args)
    {
        _actions.AddAction(uid, ref comp.GrantedAction, comp.ActionId);
    }

    private void OnShutdown(EntityUid uid, MutationFirebreathComponent comp, ComponentShutdown args)
    {
        if (comp.GrantedAction is { Valid: true } action)
        {
            _actions.RemoveAction(action);
        }
    }

    private void OnFireball(EntityUid uid, MutationFirebreathComponent comp, ProjectileSpellEvent args)
    {
        if (args.Handled) return;

        var curTime = _timing.CurTime;
        if (curTime < comp.NextUse) return;

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var fromCoords = xform.Coordinates;
        var toCoords = args.Target;

        var fromMap = _transform.ToMapCoordinates(fromCoords);
        var spawnCoords = _map.TryFindGridAt(fromMap, out var gridUid, out _)
            ? _transform.WithEntityId(fromCoords, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        var fireball = Spawn(args.Prototype, spawnCoords);

        var direction = _transform.ToMapCoordinates(toCoords).Position -
            _transform.ToMapCoordinates(spawnCoords).Position;

        var userVelocity = _physics.GetMapLinearVelocity(uid);

        _gun.ShootProjectile(fireball, direction, userVelocity, uid, uid);

        if (comp.GrantedAction is { Valid: true } action)
        {
            _audio.PlayPvs(EntityManager.GetComponentOrNull<ActionComponent>(action)?.Sound, uid);
        }

        comp.NextUse = curTime + TimeSpan.FromSeconds(comp.Cooldown);
        args.Handled = true;
    }
}
