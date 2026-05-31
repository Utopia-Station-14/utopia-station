using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Damage.Systems;
using Content.Shared.Damage.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationStupefactionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationStupefactionComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<MutationStupefactionComponent> ent, ref ComponentInit args)
    {
        ScheduleNextDrain(ent.Comp);
    }

    private void ScheduleNextDrain(MutationStupefactionComponent comp)
    {
        var delay = _random.NextFloat(comp.MinInterval, comp.MaxInterval);
        comp.NextDrainTime = _timing.CurTime + TimeSpan.FromSeconds(delay);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MutationStupefactionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextDrainTime)
                continue;

            if (!TryComp<StaminaComponent>(uid, out var stamina))
            {
                ScheduleNextDrain(comp);
                continue;
            }

            _stamina.TakeStaminaDamage(uid, comp.DrainAmount, stamina);
            ScheduleNextDrain(comp);
        }
    }
}
