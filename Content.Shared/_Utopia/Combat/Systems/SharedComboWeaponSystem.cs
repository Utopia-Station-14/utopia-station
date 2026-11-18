using System.Linq;
using Content.Shared.Body;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Utopia.Combat;

public abstract partial class SharedWeaponComboSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;


    private static WeaponCombatAction GetWeaponAction(bool isWide, ComboWeaponStand stance)
    {
        var index = ((isWide ? 1 : 0) << 1) | (int)stance;
        return (WeaponCombatAction)index;
    }

    [SubscribeLocalEvent]
    private void OnHeavyHit(Entity<ComboWeaponComponent> entity, ref MeleeHitEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!args.IsHit || !args.HitEntities.Any())
            return;

        AddAction(entity.Owner, entity.Comp, GetWeaponAction(args.Iswide, entity.Comp.CurrentStand));
        TryDoCombo(args.User, args.HitEntities[0], entity.Comp);
    }

    private bool TryDoCombo(EntityUid user, EntityUid target, ComboWeaponComponent comp)
    {
        var mainList = comp.CurrestActions;
        if (mainList == null)
            return false;

        var isComboCompleted = false;

        foreach (var combo in comp.AvailableMoves)
        {
            var protoCombo = _prototype.Index(combo);
            var subList = protoCombo.ActionsNeeds;

            if (!SharedComboSystem.ContainsSubsequence(mainList, subList))
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

    private void AddAction(EntityUid weapon, ComboWeaponComponent comp, WeaponCombatAction action)
    {
        comp.CurrestActions.Add(action);

        if (comp.CurrestActions.Count > 5)
        {
            comp.CurrestActions.RemoveAt(0);
        }

        Dirty(weapon, comp);
    }

    private void ClearActions(EntityUid weapon, ComboWeaponComponent comp)
    {
        comp.CurrestActions.Clear();
        Dirty(weapon, comp);
    }
}
