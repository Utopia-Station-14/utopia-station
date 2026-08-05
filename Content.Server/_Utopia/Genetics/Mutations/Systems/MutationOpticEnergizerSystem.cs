using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared._Utopia.Genetics.Events;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationOpticEnergizerSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationOpticEnergizerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MutationOpticEnergizerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MutationOpticEnergizerComponent, OpticBlastActionEvent>(OnBlast);
    }

    private void OnInit(Entity<MutationOpticEnergizerComponent> ent, ref ComponentInit args)
    {
        _actions.AddAction(ent, ref ent.Comp.GrantedAction, ent.Comp.ActionId);
    }

    private void OnShutdown(Entity<MutationOpticEnergizerComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.GrantedAction is { Valid: true } action)
        {
            _actions.RemoveAction(action);
        }
    }

    private void OnBlast(Entity<MutationOpticEnergizerComponent> ent, ref OpticBlastActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        if (!TryComp(ent, out TransformComponent? xform))
            return;

        var from = _transformSystem.GetMapCoordinates(ent, xform);
        var to = args.Target.Position;

        var direction = to - from.Position;

        if (direction.LengthSquared() <= 0)
            return;

        if (ent.Comp.GrantedAction is { Valid: true } action)
        {
            _audio.PlayPvs(EntityManager.GetComponentOrNull<ActionComponent>(action)?.Sound, ent);
        }

        var fromCoords = xform.Coordinates;
        var hitscanEnt = Spawn(ent.Comp.LaserProto, fromCoords);

        EnsureComp<GunComponent>(ent, out var gunComp);
        _gun.Shoot((ent, gunComp), hitscanEnt, fromCoords, args.Target, out _, ent);

        RemCompDeferred<GunComponent>(ent);
    }
}
