using Content.Shared._Utopia.ModSuits;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server._Utopia.ModSuits.Systems;
public sealed partial class ModSuitSystem
{
    private readonly List<Entity<ModSuitModuleComponent>> _moduleBuffer = new();

    private void InitializeModuleActivity()
    {
        SubscribeLocalEvent<ModSuitPartComponent, GetItemActionsEvent>(OnPartGetActions);
        SubscribeLocalEvent<ModSuitModuleComponent, EntGotRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<ModSuitComponent, ModSuitToggleModuleMessage>(OnToggleModuleMessage);
    }

    #region Main

    private void RefreshModules(Entity<ModSuitComponent> suit)
    {
        _moduleBuffer.Clear();
        CollectModules(suit, _moduleBuffer);

        foreach (var module in _moduleBuffer)
        {
            SetModuleActive(suit, module, ShouldBeActive(suit, module.Comp));
        }

        _moduleBuffer.Clear();
    }

    private bool ShouldBeActive(Entity<ModSuitComponent> suit, ModSuitModuleComponent module)
    {
        if (!module.Enabled || suit.Comp.Locked || suit.Comp.EquippedParts <= 0)
            return false;

        return module.RequiredPart is not { } part || IsDeployed(suit, part);
    }

    private void SetModuleActive(Entity<ModSuitComponent> suit, Entity<ModSuitModuleComponent> module, bool active)
    {
        if (module.Comp.Active == active)
            return;

        module.Comp.Active = active;

        if (TerminatingOrDeleted(module.Owner))
            return;

        var part = GetRequiredPart(suit, module.Comp);

        if (active)
        {
            var ev = new ModSuitModuleActivatedEvent(suit, suit.Comp.Wearer, part);
            RaiseLocalEvent(module.Owner, ref ev);
        }
        else
        {
            var ev = new ModSuitModuleDeactivatedEvent(suit, suit.Comp.Wearer, part);
            RaiseLocalEvent(module.Owner, ref ev);
        }
    }

    private static EntityUid? GetRequiredPart(Entity<ModSuitComponent> suit, ModSuitModuleComponent module)
    {
        if (module.RequiredPart is { } required && suit.Comp.Parts.TryGetValue(required, out var partUid))
            return partUid;

        return null;
    }

    private void OnModuleRemoved(Entity<ModSuitModuleComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (!ent.Comp.Active)
            return;

        var suitUid = args.Container.Owner;

        if (!TryComp<ModSuitComponent>(suitUid, out var suit) || args.Container.ID != suit.ModulesContainerId)
            return;

        SetModuleActive((suitUid, suit), ent, false);
    }

    #endregion

    #region Actions

    private void OnPartGetActions(Entity<ModSuitPartComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is { } flags && (flags & ModSuitSlots.GetSlotFlags(ent.Comp.Part)) == 0)
            return;

        if (!TryGetSuit(ent, out var suit))
            return;

        foreach (var uid in suit.Comp.ModulesContainer.ContainedEntities)
        {
            if (!TryComp<ModSuitModuleComponent>(uid, out var module)
                || !module.Toggleable
                || module.Action is not { } action
                || module.RequiredPart != ent.Comp.Part)
            {
                continue;
            }

            args.AddAction(ref module.ActionEntity, action);
        }
    }

    #endregion

    #region UI

    private void OnToggleModuleMessage(Entity<ModSuitComponent> ent, ref ModSuitToggleModuleMessage args)
    {
        var toggled = false;

        if (ent.Comp.Wearer == args.Actor && !IsLockedOut(ent, args.Actor) && !IsPanelOpen(ent, args.Actor))
        {
            var uid = GetEntity(args.Module);

            if (uid.IsValid()
                && TryComp<ModSuitModuleComponent>(uid, out var module)
                && TryToggleModule(ent, new Entity<ModSuitModuleComponent>(uid, module), out var enabled))
            {
                toggled = true;
                _popup.PopupEntity(GetToggleText(uid, enabled), args.Actor, args.Actor);
            }
        }

        if (!toggled)
            UpdateUi(ent);
    }

    #endregion
}
