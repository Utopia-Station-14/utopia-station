using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Forensics;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationChronicReagentVomitSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VomitSystem _vomit = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private ForensicsSystem _forensics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MutationChronicReagentVomitComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<MutationChronicReagentVomitComponent> ent, ref ComponentInit args)
    {
        ScheduleNextVomit(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MutationChronicReagentVomitComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextVomitTime)
                continue;

            if (!_random.Prob(comp.Chance))
            {
                ScheduleNextVomit(comp);
                continue;
            }

            PerformVomit(uid, comp);
            ScheduleNextVomit(comp);
        }
    }

    private void PerformVomit(EntityUid uid, MutationChronicReagentVomitComponent comp)
    {
        var amount = FixedPoint2.New(_random.Next(comp.MinAmount, comp.MaxAmount));

        var solution = new Solution();
        solution.AddReagent(comp.Reagent, amount);

        _vomit.Vomit(uid, thirstAdded: -30f, hungerAdded: -30f);

        if (TryComp(uid, out TransformComponent? xform))
        {
            if (_puddle.TrySpillAt(xform.Coordinates, solution, out var puddleUid))
            {
                _forensics.TransferDna(puddleUid, uid, false);
            }
        }
    }

    private void ScheduleNextVomit(MutationChronicReagentVomitComponent comp)
    {
        var delay = TimeSpan.FromSeconds(_random.NextFloat(comp.MinInterval, comp.MaxInterval));
        comp.NextVomitTime = _timing.CurTime + delay;
    }
}
