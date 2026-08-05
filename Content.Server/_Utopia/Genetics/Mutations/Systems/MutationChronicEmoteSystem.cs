using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Chat.Systems;
using Content.Server.Popups;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationChronicCoughSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MutationChronicEmoteComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, MutationChronicEmoteComponent comp, ComponentInit args)
    {
        comp.NextCheck = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0f, comp.Interval));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MutationChronicEmoteComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextCheck)
                continue;

            comp.NextCheck = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0.8f * comp.Interval, 1.2f * comp.Interval));

            if (!_random.Prob(comp.EmoteChance))
                continue;

            if (!_prototypeManager.TryIndex<EmotePrototype>(comp.EmoteId, out var _))
                continue;

            _chat.TryEmoteWithChat(uid, comp.EmoteId);

            if (!_random.Prob(comp.DropChance))
                continue;

            if (!TryComp<HandsComponent>(uid, out var hands) || hands.ActiveHandId == null)
                continue;

            var handsItem = _hands.GetActiveItem((uid, hands));

            if (handsItem is not { } _)
                continue;

            _hands.DoDrop((uid, hands), hands.ActiveHandId, false, false);
            _popup.PopupEntity("You drop what you were holding", uid, uid);
        }
    }
}
