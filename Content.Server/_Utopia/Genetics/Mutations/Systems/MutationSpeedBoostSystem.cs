using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

[Access(typeof(MovementSpeedModifierSystem))]
public sealed partial class MutationSpeedBoostSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _moveSpeedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationSpeedBoostComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MutationSpeedBoostComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<MutationSpeedBoostComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);
    }

    private void OnInit(Entity<MutationSpeedBoostComponent> ent, ref ComponentInit args)
    {
        _moveSpeedSystem.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRemove(Entity<MutationSpeedBoostComponent> ent, ref ComponentRemove args)
    {
        _moveSpeedSystem.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshMovement(Entity<MutationSpeedBoostComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.WalkMultiplier, ent.Comp.SprintMultiplier);
    }
}
