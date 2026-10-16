using Content.Server._Utopia.Genetics.Components;
using Content.Server._Utopia.Genetics.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Genetics;
using Robust.Shared.Random;

namespace Content.Server.EntityEffects.Effects.Genetics;

public sealed partial class MutationRemovalSystem : EntityEffectSystem<GeneticsComponent, MutationRemoval>
{
    [Dependency] private IEntityManager _entMan = default!;

    protected override void Effect(Entity<GeneticsComponent> entity, ref EntityEffectEvent<MutationRemoval> args)
    {
        var geneticsSystem = _entMan.System<GeneticsSystem>();
        var random = IoCManager.Resolve<IRobustRandom>();

        var scale = args.Scale;
        var attempts = random.Next(args.Effect.MinRemovals, args.Effect.MaxRemovals + 1);

        var removalsApplied = 0;

        for (var i = 0; i < attempts; i++)
        {
            if (random.Prob(args.Effect.Chance * scale))
            {
                geneticsSystem.RemoveRandomMutation((entity.Owner, entity.Comp), true);
                removalsApplied++;
            }
        }
    }
}
