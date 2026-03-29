using System.Linq;
using Content.Shared._Utopia.Grab;
using Content.Shared.Actions.Events;
using Content.Shared.CombatMode;
using Content.Shared.Coordinates;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

public sealed class SharedComboSystem : EntitySystem
{
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboComponent, DisarmAttemptEvent>(OnDisarmUsed);
        SubscribeLocalEvent<ComboComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<ComboComponent, GrabStageChangedEvent>(OnGrab);
        SubscribeLocalEvent<ComboComponent, ToggleCombatActionEvent>(OnCombatToggled);
    }

    private void OnDisarmUsed(EntityUid uid, ComboComponent comp, DisarmAttemptEvent args)
    {
        if (args.DisarmerUid != uid || args.DisarmerUid == args.TargetUid)
            return;

        comp.CurrestActions.Add(CombatAction.Disarm);

        if (comp.CurrestActions.Count >= 5)
        {
            comp.CurrestActions.RemoveAt(0);
        }

        comp.Target = args.DisarmerUid;
        TryDoCombo(args.DisarmerUid, args.TargetUid, comp);
    }

    private void OnMeleeHit(EntityUid uid, ComboComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || !args.HitEntities.Any())
            return;

        if (!HasComp<HumanoidAppearanceComponent>(args.HitEntities[0]))
            return;

        comp.CurrestActions.Add(CombatAction.Hit);

        if (comp.CurrestActions.Count >= 5 && comp.CurrestActions != null)
        {
            comp.CurrestActions.RemoveAt(0);
        }

        comp.Target = args.HitEntities[0];
        TryDoCombo(uid, comp.Target.Value, comp);
    }

    private void OnGrab(EntityUid uid, ComboComponent comp, ref GrabStageChangedEvent args)
    {
        if (args.Puller.Owner != uid || args.NewStage <= args.OldStage)
            return;

        comp.CurrestActions.Add(CombatAction.Grab);

        if (comp.CurrestActions.Count >= 5)
        {
            comp.CurrestActions.RemoveAt(0);
        }

        comp.Target = args.Pulling.Owner;
        TryDoCombo(args.Puller.Owner, comp.Target.Value, comp);
    }

    private void OnCombatToggled(EntityUid uid, ComboComponent comp, ToggleCombatActionEvent args)
    {
        if (!HasComp<CombatModeComponent>(uid))
            return;

        comp.CurrestActions.Clear();
        comp.Target = null;
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
