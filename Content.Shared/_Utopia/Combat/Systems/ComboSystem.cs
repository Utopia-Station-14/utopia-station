using System.Linq;
using Content.Shared._Utopia.Grab;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode;
using Content.Shared.Humanoid;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

public sealed class SharedComboSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboComponent, DisarmAttemptEvent>(OnDisarmUsed);
        SubscribeLocalEvent<ComboComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ComboComponent, GrabStageChangedEvent>(OnGrab);
        SubscribeLocalEvent<ComboComponent, ToggleCombatActionEvent>(OnCombatToggled);
    }

    private void OnDisarmUsed(Entity<ComboComponent> entity, ref DisarmAttemptEvent args)
    {
        if (args.DisarmerUid != entity.Owner || args.DisarmerUid == args.TargetUid)
            return;

        entity.Comp.CurrestActions.Add(CombatAction.Disarm);

        if (entity.Comp.CurrestActions.Count >= 5)
        {
            entity.Comp.CurrestActions.RemoveAt(0);
        }

        TryDoCombo(entity.Owner, args.TargetUid, entity.Comp);
    }

    private void OnMeleeHit(Entity<ComboComponent> entity, ref MeleeHitEvent args)
    {
        if (args.User != entity.Owner || !args.IsHit || !args.HitEntities.Any())
            return;

        if (!HasComp<HumanoidAppearanceComponent>(args.HitEntities[0]))
            return;

        entity.Comp.CurrestActions.Add(CombatAction.Hit);

        if (entity.Comp.CurrestActions.Count >= 5 && entity.Comp.CurrestActions != null)
        {
            entity.Comp.CurrestActions.RemoveAt(0);
        }

        TryDoCombo(entity.Owner, args.HitEntities[0], entity.Comp);
    }

    private void OnGrab(Entity<ComboComponent> entity, ref GrabStageChangedEvent args)
    {
        if (args.Puller.Owner != entity.Owner || args.NewStage <= args.OldStage)
            return;

        entity.Comp.CurrestActions.Add(CombatAction.Grab);

        if (entity.Comp.CurrestActions.Count >= 5)
        {
            entity.Comp.CurrestActions.RemoveAt(0);
        }

        TryDoCombo(entity.Owner, args.Pulling.Owner, entity.Comp);
    }

    private void OnCombatToggled(Entity<ComboComponent> entity, ref ToggleCombatActionEvent args)
    {
        if (!HasComp<CombatModeComponent>(entity))
            return;

        entity.Comp.CurrestActions.Clear();
    }

    private bool TryDoCombo(EntityUid user, EntityUid target, ComboComponent comp)
    {
        var mainList = comp.CurrestActions;
        if (mainList == null)
            return false;

        var isComboCompleted = false;

        foreach (var combo in comp.AvailableMoves)
        {
            var protoCombo = _prototype.Index(combo);
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
            comp.CurrestActions.Clear();
        }

        return true;
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
