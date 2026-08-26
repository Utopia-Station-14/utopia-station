using System.Linq;
using Content.Server._Utopia.Genetics.Components;
using Content.Server.Popups;
using Content.Shared._Utopia.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Systems;

public sealed partial class InstabilityCountdownSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GeneticsSystem _genetics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PendingInstabilityMutationComponent, ComponentStartup>(OnStartup);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<PendingInstabilityMutationComponent>();

        while (query.MoveNext(out var uid, out var pending))
        {
            var remaining = pending.EndTime - curTime;

            if (remaining <= TimeSpan.Zero)
            {
                TriggerMutation(uid);
                RemComp<PendingInstabilityMutationComponent>(uid);
                continue;
            }

            var totalDuration = pending.EndTime - pending.StartTime;

            if (remaining <= TimeSpan.FromSeconds(10) && !pending.Warning10Sec)
            {
                _popup.PopupEntity(Loc.GetString("genetics-instability-warning-10sec"), uid, uid);
                pending.Warning10Sec = true;
            }

            if (remaining <= totalDuration / 2 && !pending.WarningHalfway)
            {
                _popup.PopupEntity(Loc.GetString("genetics-instability-warning-half"), uid, uid);
                pending.WarningHalfway = true;
            }

            if (remaining <= totalDuration - TimeSpan.FromSeconds(10) && !pending.WarningStart)
            {
                _popup.PopupEntity(Loc.GetString("genetics-instability-warning-start"), uid, uid);
                pending.WarningStart = true;
            }
        }
    }

    private void OnStartup(EntityUid uid, PendingInstabilityMutationComponent pending, ComponentStartup args)
    {
        pending.StartTime = _timing.CurTime;
    }

    private void TriggerMutation(EntityUid uid)
    {
        if (!TryComp<GeneticsComponent>(uid, out var genetics))
            return;

        var validProtos = new List<GeneticMutationPrototype>();
        foreach (var proto in _proto.EnumeratePrototypes<GeneticMutationPrototype>())
        {
            if (!proto.InstabilityMutation)
                continue;

            if (!_genetics.CanEntityReceiveMutation(uid, proto, false))
                continue;

            if (genetics.Mutations.Any(m => m.Id == proto.ID && m.Enabled))
                continue;

            validProtos.Add(proto);
        }

        if (validProtos.Count == 0)
            return;

        var chosenProto = _random.Pick(validProtos);

        foreach (var conflictId in chosenProto.Conflicts)
        {
            var conflictEntry = genetics.Mutations.FirstOrDefault(m => m.Id == conflictId);
            if (conflictEntry != null && conflictEntry.Enabled)
            {
                _genetics.TryDeactivateMutation((uid, genetics), conflictId);
            }
        }

        if (!genetics.Mutations.Any(m => m.Id == chosenProto.ID))
        {
            _genetics.TryAddMutation((uid, genetics), chosenProto.ID);
        }

        _genetics.TryActivateMutation((uid, genetics), chosenProto.ID);
    }
}
