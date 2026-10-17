using Content.Shared._Utopia.ModSuits;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Clothing;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private void LockSuit(Entity<ModSuitComponent> ent)
    {
        if (ent.Comp.EquippedParts == 0 || ent.Comp.Locked)
            return;

        ent.Comp.Locked = true;

        if (ent.Comp.Wearer is { } wearer)
            _popup.PopupEntity(Loc.GetString("modsuit-energy-depleted-locked"), wearer, wearer);

        RefreshSpeed(ent);
        UpdateDraw(ent);
        UpdateUi(ent);
    }

    #region Speed
    private void OnToggleSpeedMessage(Entity<ModSuitComponent> ent, ref ModSuitToggleSpeedMessage args)
    {
        if (ent.Comp.Wearer != args.Actor || IsLockedOut(ent, args.Actor))
            return;

        if (!ent.Comp.SpeedEnabled && !IsDeployed(ent, ModSuitPart.Body))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-speed-needs-body"), args.Actor, args.Actor);
            return;
        }

        ent.Comp.SpeedEnabled = !ent.Comp.SpeedEnabled;
        RefreshSpeed(ent);
        UpdateDraw(ent);
        UpdateUi(ent);
    }

    private bool IsSpeedActive(Entity<ModSuitComponent> ent)
    {
        return ent.Comp.SpeedEnabled
            && !ent.Comp.Locked
            && IsDeployed(ent, ModSuitPart.Body);
    }

    private void RefreshSpeed(Entity<ModSuitComponent> ent)
    {
        if (ent.Comp.Wearer is { } wearer && !TerminatingOrDeleted(wearer))
            _speed.RefreshMovementSpeedModifiers(wearer);
    }

    private void OnRefreshSpeed(Entity<ModSuitComponent> ent, ref InventoryRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        if (!IsSpeedActive(ent))
            return;

        if (!ent.Comp.Parts.TryGetValue(ModSuitPart.Body, out var bodyUid)
            || !TryComp<ClothingSpeedModifierComponent>(bodyUid, out var bodySpeed))
            return;

        var walk = bodySpeed.WalkModifier > 0f ? 1f / bodySpeed.WalkModifier : 1f;
        var sprint = bodySpeed.SprintModifier > 0f ? 1f / bodySpeed.SprintModifier : 1f;

        args.Args.ModifySpeed(walk, sprint);
    }
    #endregion
}
