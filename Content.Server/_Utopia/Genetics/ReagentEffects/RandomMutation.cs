using Content.Server._Utopia.Genetics.Components;
using Content.Server._Utopia.Genetics.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Genetics;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Genetics.ReagentEffects;

public sealed partial class RandomMutationSystem : EntityEffectSystem<GeneticsComponent, RandomMutation>
{
    [Dependency] private IEntityManager _entMan = default!;

    protected override void Effect(Entity<GeneticsComponent> entity, ref EntityEffectEvent<RandomMutation> args)
    {
        var geneticsSystem = _entMan.System<GeneticsSystem>();
        var random = IoCManager.Resolve<IRobustRandom>();

        var scale = args.Scale;
        var attempts = random.Next(args.Effect.MinMutations, args.Effect.MaxMutations + 1);

        var mutationsApplied = 0;

        for (var i = 0; i < attempts; i++)
        {
            if (random.Prob(args.Effect.Chance * scale))
            {
                geneticsSystem.TriggerRandomMutation((entity.Owner, entity.Comp));
                mutationsApplied++;
            }
        }
    }
}
