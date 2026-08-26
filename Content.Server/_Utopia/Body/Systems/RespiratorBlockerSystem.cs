using Content.Shared.StatusEffectNew;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.RespiratorBlocker;

public sealed partial class RespiratorBlockSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BreathBlockStatusEffectComponent, StatusEffectAppliedEvent>(OnBreathStatusApplied);
        SubscribeLocalEvent<BreathBlockStatusEffectComponent, StatusEffectRemovedEvent>(OnBreathStatusRemoved);
    }

    private void OnBreathStatusApplied(Entity<BreathBlockStatusEffectComponent> entity, ref StatusEffectAppliedEvent args)
    {
        if (_gameTiming.ApplyingState)
            return;

        EnsureComp<BreathBlockComponent>(args.Target);
    }

    private void OnBreathStatusRemoved(Entity<BreathBlockStatusEffectComponent> entity, ref StatusEffectRemovedEvent args)
    {
        if (!HasComp<BreathBlockComponent>(args.Target))
            return;

        RemComp<BreathBlockComponent>(args.Target);
    }
}
