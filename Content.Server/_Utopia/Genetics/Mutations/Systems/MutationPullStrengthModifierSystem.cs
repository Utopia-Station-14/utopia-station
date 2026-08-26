using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationPullStrengthModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationPullStrengthModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(Entity<MutationPullStrengthModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling == null)
            return;

        args.ModifySpeed(args.WalkSpeedModifier * ent.Comp.PullSlowdownMultiplier,
            args.SprintSpeedModifier * ent.Comp.PullSlowdownMultiplier);
    }
}
