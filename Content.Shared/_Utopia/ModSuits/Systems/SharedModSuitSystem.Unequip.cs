using Content.Shared.DoAfter;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Utopia.ModSuits;

public abstract partial class SharedModSuitSystem
{
    private void InitializeUnequip()
    {
        SubscribeLocalEvent<ModSuitComponent, BeingUnequippedAttemptEvent>(OnSuitUnequipAttempt);
        SubscribeLocalEvent<ModSuitPartComponent, BeingUnequippedAttemptEvent>(OnPartUnequipAttempt);
    }

    private void OnSuitUnequipAttempt(Entity<ModSuitComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (ent.Comp.EquippedParts == 0)
            return;

        args.Reason = ent.Comp.Locked ? "modsuit-locked-no-energy" : "modsuit-remove-all-attached-first";
        args.Cancel();
    }

    private void OnPartUnequipAttempt(Entity<ModSuitPartComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (ent.Comp.Retracting)
            return;

        args.Cancel();

        if (!TryGetSuit(ent, out var suit))
            return;

        if (!suit.Comp.Locked)
        {
            args.Reason = "modsuit-part-use-ui";
            return;
        }

        if (suit.Comp.Wearer is not { } user)
            return;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            suit.Comp.ManualRemoveDelay,
            new ModSuitManualRemoveDoAfterEvent(ent.Comp.Part),
            suit.Owner,
            target: suit.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            args.Reason = "modsuit-manual-remove-started";
    }
}
