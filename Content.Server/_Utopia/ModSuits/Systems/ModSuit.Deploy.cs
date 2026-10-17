using System.Linq;
using Content.Shared._Utopia.ModSuits;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private void OnSuitEquipped(Entity<ModSuitComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.BACK) == 0)
            return;

        ent.Comp.Wearer = args.EquipTarget;
        UpdateUi(ent);
    }

    private void OnSuitUnequipped(Entity<ModSuitComponent> ent, ref GotUnequippedEvent args)
    {
        var oldWearer = ent.Comp.Wearer;

        if (oldWearer is { } wearer)
            RetractAll(ent, wearer);

        ent.Comp.Wearer = null;
        ent.Comp.SpeedEnabled = false;
        _deploying.Remove(ent);
        _ui.CloseUi(ent.Owner, ModSuitUiKey.Key);

        if (oldWearer is { } w && !TerminatingOrDeleted(w))
            _speed.RefreshMovementSpeedModifiers(w);
    }

    private void OnSuitUnequipAttempt(Entity<ModSuitComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (ent.Comp.EquippedParts == 0)
            return;

        args.Reason = ent.Comp.Locked ? "modsuit-locked-no-energy" : "modsuit-remove-all-attached-first";
        args.Cancel();
    }

    private void OnGetActions(Entity<ModSuitComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is { } flags && (flags & SlotFlags.BACK) == 0)
            return;

        args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnPartUnequipAttempt(Entity<ModSuitPartComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (ent.Comp.Retracting)
            return;

        args.Cancel();

        if (TerminatingOrDeleted(ent.Comp.Suit) || !TryComp<ModSuitComponent>(ent.Comp.Suit, out var suit))
            return;

        if (!suit.Locked)
        {
            args.Reason = "modsuit-part-use-ui";
            return;
        }

        if (suit.Wearer is not { } user)
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            suit.ManualRemoveDelay,
            new ModSuitManualRemoveDoAfterEvent(ent.Comp.Part),
            ent.Comp.Suit,
            target: ent.Comp.Suit)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Reason = "modsuit-manual-remove-started";
    }

    private void OnManualRemoveDoAfter(Entity<ModSuitComponent> ent, ref ModSuitManualRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!ent.Comp.Locked || ent.Comp.Wearer is not { } wearer)
            return;

        Retract(ent, wearer, args.Part);
        UpdateUi(ent);
    }

    private void OnPartUnequipped(Entity<ModSuitPartComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Retracting || TerminatingOrDeleted(ent))
            return;

        _pendingReturn.Add(ent);
    }

    private void OnTogglePartMessage(Entity<ModSuitComponent> ent, ref ModSuitTogglePartMessage args)
    {
        if (ent.Comp.Wearer != args.Actor || IsLockedOut(ent, args.Actor) || IsPanelOpen(ent, args.Actor))
            return;

        if (IsDeployed(ent, args.Part))
            TryRetractPaid(ent, args.Actor, args.Part);
        else
            StartDeploy(ent, args.Actor, args.Part, chain: false);

        UpdateUi(ent);
    }

    private void OnToggleAllMessage(Entity<ModSuitComponent> ent, ref ModSuitToggleAllMessage args)
    {
        if (ent.Comp.Wearer is not { } wearer || wearer != args.Actor || IsLockedOut(ent, args.Actor)
            || IsPanelOpen(ent, args.Actor))
            return;

        if (ent.Comp.EquippedParts == 0)
        {
            if (NextUndeployed(ent) is { } first)
                StartDeploy(ent, wearer, first, chain: true);
        }
        else
        {
            foreach (var part in ent.Comp.Parts.Keys.ToArray())
            {
                if (!IsDeployed(ent, part))
                    continue;

                if (!TryRetractPaid(ent, wearer, part))
                    break;
            }
        }

        UpdateUi(ent);
    }

    private bool TryRetractPaid(Entity<ModSuitComponent> ent, EntityUid wearer, ModSuitPart part)
    {
        if (!HasEnergy(ent, ent.Comp.PartSwapCost))
        {
            LockSuit(ent);
            return false;
        }

        if (!Retract(ent, wearer, part))
            return false;

        SpendEnergy(ent, ent.Comp.PartSwapCost);
        return true;
    }

    private bool IsDeployed(Entity<ModSuitComponent> ent, ModSuitPart part)
    {
        return ent.Comp.Parts.TryGetValue(part, out var uid) && !ent.Comp.PartsContainer.Contains(uid);
    }

    private void RefreshCount(Entity<ModSuitComponent> ent)
    {
        var count = 0;
        foreach (var part in ent.Comp.Parts.Keys)
        {
            if (IsDeployed(ent, part))
                count++;
        }

        ent.Comp.EquippedParts = count;
        if (count == 0)
            ent.Comp.Locked = false;

        if (!IsDeployed(ent, ModSuitPart.Boots))
            ent.Comp.SpeedEnabled = false;

        RefreshSpeed(ent);
        UpdateDraw(ent);
    }

    private ModSuitPart? NextUndeployed(Entity<ModSuitComponent> ent)
    {
        foreach (var (part, uid) in ent.Comp.Parts)
        {
            if (ent.Comp.PartsContainer.Contains(uid))
                return part;
        }
        return null;
    }

    private bool StartDeploy(Entity<ModSuitComponent> ent, EntityUid wearer, ModSuitPart part, bool chain)
    {
        if (_deploying.Contains(ent))
            return false;

        if (!ent.Comp.Parts.TryGetValue(part, out var partUid) || !ent.Comp.PartsContainer.Contains(partUid))
            return false;

        if (ent.Comp.DeployDelay <= 0f)
        {
            var ok = Deploy(ent, part);
            if (ok && chain && NextUndeployed(ent) is { } next)
                StartDeploy(ent, wearer, next, chain: true);
            return ok;
        }

        var args = new DoAfterArgs(
            EntityManager,
            wearer,
            ent.Comp.DeployDelay,
            new ModSuitDeployDoAfterEvent(part, chain),
            ent,
            target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _deploying.Add(ent);
        return true;
    }

    private void OnDeployDoAfter(Entity<ModSuitComponent> ent, ref ModSuitDeployDoAfterEvent args)
    {
        _deploying.Remove(ent);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Wearer != args.User || ent.Comp.Locked || ent.Comp.PanelOpen)
            return;

        if (!Deploy(ent, args.Part))
        {
            UpdateUi(ent);
            return;
        }

        if (args.Chain && NextUndeployed(ent) is { } next)
            StartDeploy(ent, args.User, next, chain: true);

        UpdateUi(ent);
    }

    private bool Deploy(Entity<ModSuitComponent> ent, ModSuitPart part)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return false;

        if (ent.Comp.PanelOpen)
            return false;

        if (!ent.Comp.Parts.TryGetValue(part, out var partUid) || !ent.Comp.PartsContainer.Contains(partUid))
            return false;

        if (!TryComp<ModSuitPartComponent>(partUid, out var partComp))
            return false;

        if (!HasEnergy(ent, ent.Comp.PartSwapCost))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-no-energy"), wearer, wearer);
            return false;
        }

        var slot = Slots[part];
        var stashed = StashSuitStorage(wearer, slot);

        EntityUid? replaced = null;
        if (_inventory.TryGetSlotEntity(wearer, slot, out var occupied))
        {
            if (!_inventory.TryUnequip(wearer, wearer, slot, out replaced, silent: true)
                || replaced is not { } removed)
            {
                _popup.PopupEntity(Loc.GetString("modsuit-remove-first", ("entity", occupied.Value)), wearer, wearer);
                RestoreSuitStorage(wearer, stashed);
                return false;
            }

            _container.Insert(removed, partComp.Replaced);
        }

        if (!_inventory.TryEquip(wearer, wearer, partUid, slot, silent: true, force: true))
        {
            if (replaced is { } old)
                _inventory.TryEquip(wearer, wearer, old, slot, silent: true, force: true);

            RestoreSuitStorage(wearer, stashed);
            return false;
        }

        RestoreSuitStorage(wearer, stashed);
        RefreshCount(ent);

        SpendEnergy(ent, ent.Comp.PartSwapCost);
        return true;
    }

    private bool Retract(Entity<ModSuitComponent> ent, EntityUid wearer, ModSuitPart part)
    {
        if (!ent.Comp.Parts.TryGetValue(part, out var partUid) || ent.Comp.PartsContainer.Contains(partUid))
            return false;

        if (!TryComp<ModSuitPartComponent>(partUid, out var partComp))
            return false;

        var slot = Slots[part];

        partComp.Retracting = true;
        _pendingReturn.Remove(partUid);

        var worn = _inventory.TryGetSlotEntity(wearer, slot, out var inSlot) && inSlot == partUid;
        var stashed = worn ? StashSuitStorage(wearer, slot) : null;

        if (worn)
            _inventory.TryUnequip(wearer, slot, silent: true, force: true);

        _container.Insert(partUid, ent.Comp.PartsContainer);
        partComp.Retracting = false;

        if (partComp.Replaced.ContainedEntity is { Valid: true } stored
            && !_inventory.TryGetSlotEntity(wearer, slot, out _))
        {
            _inventory.TryEquip(wearer, wearer, stored, slot, silent: true, force: true);
        }

        RestoreSuitStorage(wearer, stashed);
        RefreshCount(ent);
        return true;
    }

    private EntityUid? StashSuitStorage(EntityUid wearer, string slot)
    {
        if (slot != "outerClothing")
            return null;

        if (!_inventory.TryUnequip(wearer, "suitstorage", out var stashed, silent: true, force: true))
            return null;

        return stashed;
    }

    private void RestoreSuitStorage(EntityUid wearer, EntityUid? stashed)
    {
        if (stashed is { } item)
            _inventory.TryEquip(wearer, wearer, item, "suitstorage", silent: true, force: true);
    }

    private void RetractAll(Entity<ModSuitComponent> ent, EntityUid wearer)
    {
        foreach (var part in ent.Comp.Parts.Keys.ToArray())
            Retract(ent, wearer, part);
    }

    private bool IsLockedOut(Entity<ModSuitComponent> ent, EntityUid wearer)
    {
        if (!ent.Comp.Locked)
            return false;

        _popup.PopupEntity(Loc.GetString("modsuit-locked-no-energy"), wearer, wearer);
        return true;
    }
}
