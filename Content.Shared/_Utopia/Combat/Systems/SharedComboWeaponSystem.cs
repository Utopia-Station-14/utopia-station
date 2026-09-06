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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboWeaponComponent, MeleeHitEvent>(OnHeavyHit);
    }

    private static WeaponCombatAction GetWeaponAction(bool isWide, ComboWeaponStand stance)
    {
        var index = ((isWide ? 1 : 0) << 1) | (int)stance;
        return (WeaponCombatAction)index;
    }

    private void OnHeavyHit(Entity<ComboWeaponComponent> entity, ref MeleeHitEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!args.IsHit || !args.HitEntities.Any())
            return;

        if (!HasComp<VisualBodyComponent>(args.HitEntities[0]))
            return;

        var move = GetWeaponAction(args.Iswide, entity.Comp.CurrentStand);
        entity.Comp.CurrestActions.Add(move);

        if (entity.Comp.CurrestActions.Count >= 5 && entity.Comp.CurrestActions != null)
        {
            entity.Comp.CurrestActions.RemoveAt(0);
        }

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
            comp.CurrestActions.Clear();
        }

        return true;
    }
}
