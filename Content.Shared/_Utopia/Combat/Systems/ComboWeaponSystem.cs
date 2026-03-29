using System.Linq;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared.Humanoid;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Utopia.Combat;

public sealed class SharedWeaponComboSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboWeaponComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<ComboWeaponComponent, MeleeHitEvent>(OnHeavyHit);
    }

    public static WeaponCombatAction GetWeaponAction(bool isWide, ComboWeaponStand stance)
    {
        var index = ((isWide ? 1 : 0) << 1) | (int)stance;
        return (WeaponCombatAction)index;
    }

    private void OnHeavyHit(EntityUid uid, ComboWeaponComponent comp, MeleeHitEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (!args.IsHit || !args.HitEntities.Any())
            return;

        if (!HasComp<HumanoidAppearanceComponent>(args.HitEntities[0]))
            return;

        var move = GetWeaponAction(args.Iswide, comp.CurrentStand);
        comp.CurrestActions.Add(move);

        if (comp.CurrestActions.Count >= 5 && comp.CurrestActions != null)
        {
            comp.CurrestActions.RemoveAt(0);
        }

        comp.Target = args.HitEntities[0];
        TryDoCombo(args.User, args.HitEntities[0], comp);
    }

    private void OnUniqueAction(Entity<ComboWeaponComponent> entity, ref UniqueActionEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        entity.Comp.CurrentStand = entity.Comp.CurrentStand switch
        {
            ComboWeaponStand.Protective => ComboWeaponStand.Offensive,
            ComboWeaponStand.Offensive => ComboWeaponStand.Protective,
            _ => entity.Comp.CurrentStand
        };

        if (!TryComp<AppearanceComponent>(entity, out var appearanceComponent))
            return;

        _appearance.SetData(entity, ComboWeaponState.State, entity.Comp.CurrentStand == ComboWeaponStand.Offensive,
            appearanceComponent);

        Dirty(entity);

        if (entity.Comp.SwapSound != null)
        {
            _audio.PlayPredicted(entity.Comp.SwapSound, entity, args.UserUid);
        }
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
