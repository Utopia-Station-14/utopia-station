using Content.Server._Utopia.Genetics.Components;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared._Utopia.Genetics;
using Content.Shared._Utopia.Genetics.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Systems;

public sealed partial class DNASequenceInjectorSystem : EntitySystem
{
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private GeneticShuffleSystem _shuffle = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private GeneticsSystem _genetics = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DnaSequenceInjectorComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<DnaSequenceInjectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<DnaSequenceInjectorComponent, DNASequenceInjectorDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<DnaSequenceInjectorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DnaSequenceInjectorComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.MutationId == null)
        {
            args.PushMarkup(Loc.GetString("dna-injector-examine-empty"));
            return;
        }

        var type = ent.Comp.IsMutator ? "mutator" : "activator";
        var name = _prototype.TryIndex<GeneticMutationPrototype>(ent.Comp.MutationId, out var proto)
            ? Loc.GetString(proto.Name)
            : ent.Comp.MutationId;

        args.PushMarkup(Loc.GetString($"dna-injector-examine-{type}", ("mutation", name)));
    }

    private void OnAfterInteract(Entity<DnaSequenceInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { Valid: true } target || !args.CanReach || args.Handled)
            return;

        if (ent.Comp.MutationId == null)
        {
            args.Handled = true;
            return;
        }

        var user = args.User;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, 2f, new DNASequenceInjectorDoAfterEvent(), ent, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTarget
        };

        if (user != target)
        {
            _popup.PopupEntity(Loc.GetString("dna-injector-start-other", ("user", Name(user))), target, target);
            _popup.PopupEntity(Loc.GetString("dna-injector-start", ("user", Name(user))), user, user);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("dna-injector-start-self"), user, user);
        }

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnUseInHand(Entity<DnaSequenceInjectorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.MutationId == null)
        {
            args.Handled = true;
            return;
        }

        var user = args.User;
        var target = args.User;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, 2f, new DNASequenceInjectorDoAfterEvent(), ent, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTarget
        };

        _popup.PopupEntity(Loc.GetString("dna-injector-start-self"), user, user);

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDoAfter(Entity<DnaSequenceInjectorComponent> ent, ref DNASequenceInjectorDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target || args.Handled)
            return;

        if (TryInject(ent, target, args.User, ent.Comp))
        {
            args.Handled = true;
        }
    }

    private bool TryInject(EntityUid injector, EntityUid targetUid, EntityUid user, DnaSequenceInjectorComponent comp)
    {
        if (comp.MutationId is not { } mutationId)
            return false;

        if (!TryComp<GeneticsComponent>(targetUid, out var genetics))
            return false;

        if (!_prototype.HasIndex<GeneticMutationPrototype>(mutationId))
            return false;

        if (!_shuffle.HasSlot(mutationId))
        {
            _popup.PopupEntity(Loc.GetString("dna-injector-no-effect"), targetUid, user);
            Del(injector);
            return false;
        }

        bool success;

        if (comp.IsMutator)
        {
            success = _genetics.TryAddMutation((targetUid, genetics), mutationId) &&
                _genetics.TryActivateMutation((targetUid, genetics), mutationId);
        }
        else
        {
            success = _genetics.TryActivateMutation((targetUid, genetics), mutationId);
        }

        if (!success)
        {
            _popup.PopupEntity(Loc.GetString("dna-injector-no-effect"), targetUid, user);
        }

        var empty = Spawn(comp.EntityEmpty, Transform(injector).Coordinates);

        if (TryComp<HandsComponent>(user, out var hands)
        && _hands.TryGetActiveItem(user, out var held)
        && held == injector)
        {
            _hands.DoDrop(user, hands.ActiveHandId!, false, false);
            _hands.DoPickup(user, hands.ActiveHandId!, empty);
        }

        Del(injector);
        return true;
    }
}
