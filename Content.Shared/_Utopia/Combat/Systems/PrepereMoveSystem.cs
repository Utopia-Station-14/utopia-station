using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared._Utopia.Combat;

public sealed class SharedPrepareActionSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrepareActionComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<PrepareActionComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<PrepareActionComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<PrepareActionComponent, PrepareMoveEvent>(OnPrepareAction);
    }

    private void OnComponentInit(Entity<PrepareActionComponent> ent, ref ComponentInit args)
    {
        if (HasComp<ComboComponent>(ent) && !ent.Comp.CanBeUsedWithCombo)
            return;

        foreach (var actionId in ent.Comp.BaseCombatMoves)
        {
            var actions = _actions.AddAction(ent, actionId);
            if (actions != null)
            {
                ent.Comp.CombatMoveEntities.Add(actions.Value);
            }
        }
    }

    private void OnComponentShutdown(Entity<PrepareActionComponent> ent, ref ComponentShutdown args)
    {
        foreach (var action in ent.Comp.CombatMoveEntities)
        {
            _actions.RemoveAction(action);
        }
    }

    private void OnMeleeHit(Entity<PrepareActionComponent> ent, ref MeleeHitEvent args)
    {
        if (ent.Comp.PreparedMove == null)
            return;

        if (!args.IsHit || !args.HitEntities.Any())
            return;

        if (!TryComp<HumanoidAppearanceComponent>(args.HitEntities[0], out _))
            return;

        UseEventOnTarget(ent, args.HitEntities[0], ent.Comp.PreparedMove);
        ent.Comp.PreparedMove = null;
    }

    private void OnPrepareAction(Entity<PrepareActionComponent> ent, ref PrepareMoveEvent args)
    {
        ent.Comp.PreparedMove = args.ComboEvents;
        _popupSystem.PopupCursor(Loc.GetString("move-ready", ("action", args.Name)), args.Performer);

        foreach (var action in ent.Comp.CombatMoveEntities)
        {
            _actions.StartUseDelay(action);
        }
    }

    public void UseEventOnTarget(EntityUid user, EntityUid target, List<IComboEffect> combo)
    {
        foreach (var comboEvent in combo)
        {
            comboEvent.DoEffect(user, target, EntityManager);
        }
    }
}
