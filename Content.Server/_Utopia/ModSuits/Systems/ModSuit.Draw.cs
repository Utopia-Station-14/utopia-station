using Content.Shared._Utopia.ModSuits;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Clothing;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private void OnCellEmpty(Entity<ModSuitComponent> ent, ref PowerCellSlotEmptyEvent args)
        => LockSuit(ent);

    private (float totalDraw, float modulesDraw, float speedDraw) CalculateDraw(Entity<ModSuitComponent> ent)
    {
        if (ent.Comp.EquippedParts <= 0 || ent.Comp.Locked)
            return (0f, 0f, 0f);

        var modulesDraw = 0f;
        foreach (var moduleUid in ent.Comp.ModulesContainer.ContainedEntities)
        {
            if (TryComp<ModSuitModuleComponent>(moduleUid, out var moduleComp) && moduleComp.Active)
                modulesDraw += moduleComp.Expenditure;
        }

        var speedDraw = IsSpeedActive(ent) ? ent.Comp.SpeedDraw : 0f;
        return (modulesDraw + speedDraw, modulesDraw, speedDraw);
    }

    private float? GetCharge(Entity<ModSuitComponent> ent)
    {
        if (!_cell.TryGetBatteryFromSlot(ent.Owner, out var battery) || battery is not { } bat)
            return null;

        return _battery.GetChargeLevel(bat.AsNullable());
    }

    private bool HasEnergy(Entity<ModSuitComponent> ent, float amount)
    {
        return _cell.TryGetBatteryFromSlot(ent.Owner, out var battery)
            && battery is { } bat
            && _battery.GetCharge(bat.AsNullable()) >= amount;
    }

    private bool SpendEnergy(Entity<ModSuitComponent> ent, float amount)
    {
        if (amount <= 0f)
            return true;

        if (!_cell.TryGetBatteryFromSlot(ent.Owner, out var battery) || battery is not { } bat)
            return false;

        if (!_battery.TryUseCharge(bat.AsNullable(), amount))
            return false;

        if (_battery.GetChargeLevel(bat.AsNullable()) <= 0f)
            LockSuit(ent);

        return true;
    }

    private void UpdateDraw(Entity<ModSuitComponent> ent)
    {
        if (!TryComp<PowerCellDrawComponent>(ent, out var draw))
            return;

        var (totalDraw, _, _) = CalculateDraw(ent);

        _cell.SetDrawRate((ent.Owner, draw), totalDraw);
        _cell.SetDrawEnabled((ent.Owner, draw), totalDraw > 0);
    }
}
