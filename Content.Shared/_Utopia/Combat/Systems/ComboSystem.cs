using System.Linq;
using Content.Shared._Utopia.Grab;
using Content.Shared.Actions.Events;
using Content.Shared.Body;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Shared._Utopia.Combat;

public sealed partial class SharedComboSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboComponent, DisarmAttemptEvent>(OnDisarmUsed);
        SubscribeLocalEvent<ComboComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ComboComponent, GrabStageChangedEvent>(OnGrab);
        SubscribeLocalEvent<ComboComponent, ToggleCombatActionEvent>(OnCombatToggled);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<ComboComponent>();
        while (query.MoveNext(out var ent, out var comp))
        {
            if (_timing.CurTime < comp.ResetTime)
                continue;

            ClearActions(ent, comp);
        }
    }

    private void OnDisarmUsed(Entity<ComboComponent> entity, ref DisarmAttemptEvent args)
    {
        if (args.DisarmerUid != entity.Owner || args.DisarmerUid == args.TargetUid)
            return;

        AddAction(entity.Owner, entity.Comp, CombatAction.Disarm);
        TryDoCombo(entity.Owner, args.TargetUid, entity.Comp);
    }

    private void OnMeleeHit(Entity<ComboComponent> entity, ref MeleeHitEvent args)
    {
        if (args.User != entity.Owner || !args.IsHit || !args.HitEntities.Any())
            return;

        if (!HasComp<VisualBodyComponent>(args.HitEntities[0]))
            return;

        AddAction(entity.Owner, entity.Comp, CombatAction.Hit);
        TryDoCombo(entity.Owner, args.HitEntities[0], entity.Comp);
    }

    private void OnGrab(Entity<ComboComponent> entity, ref GrabStageChangedEvent args)
    {
        if (args.Puller.Owner != entity.Owner || args.NewStage <= args.OldStage)
            return;

        AddAction(entity.Owner, entity.Comp, CombatAction.Grab);
        TryDoCombo(entity.Owner, args.Pulling.Owner, entity.Comp);
    }

    private void OnCombatToggled(Entity<ComboComponent> entity, ref ToggleCombatActionEvent args)
    {
        if (!HasComp<CombatModeComponent>(entity))
            return;

        ClearActions(entity.Owner, entity.Comp);
    }

    private bool TryDoCombo(EntityUid user, EntityUid target, ComboComponent comp)
    {
        var mainList = comp.CurrestActions;
        if (mainList == null)
            return false;

        var isComboCompleted = false;

        foreach (var combo in comp.AvailableMoves)
        {
            var protoCombo = ProtoMan.Index(combo);
            var subList = protoCombo.ActionsNeeds;

            if (!ContainsSubsequence(mainList, subList))
                continue;


            foreach (var comboEvent in protoCombo.ComboEvent)
            {
                comboEvent.DoEffect(user, target, EntityManager);
            }

            isComboCompleted = true;
        }

        if (isComboCompleted)
        {
            ClearActions(user, comp);
        }

        return true;
    }

    private void AddAction(EntityUid user, ComboComponent comp, CombatAction action)
    {
        comp.CurrestActions.Add(action);
        comp.ResetTime += _timing.CurTime;

        if (comp.CurrestActions.Count > 5)
        {
            comp.CurrestActions.RemoveAt(0);
        }

        Dirty(user, comp);
    }

    private void ClearActions(EntityUid user, ComboComponent comp)
    {
        comp.CurrestActions.Clear();
        Dirty(user, comp);
    }

    public static bool ContainsSubsequence<T>(List<T> mainList, List<T> subList)
    {
        if (subList.Count == 0)
            return true;

        for (var i = 0; i <= mainList.Count - subList.Count; i++)
        {
            var match = true;
            for (var j = 0; j < subList.Count; j++)
            {
                if (!EqualityComparer<T>.Default.Equals(mainList[i + j], subList[j]))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return true;
        }

        return false;
    }
}
