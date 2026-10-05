using Content.Shared._Utopia.ModSuits;
using Content.Shared.PowerCell.Components;
using Content.Shared.PowerCell;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private void OnCellEmpty(Entity<ModSuitComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        if (ent.Comp.EquippedParts == 0 || ent.Comp.Locked)
            return;

        ent.Comp.Locked = true;

        if (ent.Comp.Wearer is { } wearer)
            _popup.PopupEntity(Loc.GetString("modsuit-energy-depleted-locked"), wearer, wearer);

        UpdateUi(ent);
    }

    private void OnToggleUiAction(Entity<ModSuitComponent> ent, ref ToggleModSuitUiEvent args)
    {
        if (args.Handled || ent.Comp.Wearer != args.Performer)
            return;

        args.Handled = true;
        UpdateUi(ent);
        _ui.TryToggleUi(ent.Owner, ModSuitUiKey.Key, args.Performer);
    }

    private (float totalDraw, float baseDraw, float partsDraw, float modulesDraw) CalculateDraw(Entity<ModSuitComponent> ent)
    {
        var modulesDraw = 0f;
        foreach (var moduleUid in ent.Comp.ModulesContainer.ContainedEntities)
        {
            if (TryComp<ModSuitModuleComponent>(moduleUid, out var moduleComp) && moduleComp.Active)
            {
                modulesDraw += moduleComp.Expenditure;
            }
        }

        var partsDraw = ent.Comp.EquippedParts * ent.Comp.DrawPerPart;
        var baseDraw = (ent.Comp.EquippedParts > 0 || modulesDraw > 0) ? ent.Comp.BaseDraw : 0f;
        var totalDraw = baseDraw + partsDraw + modulesDraw;

        return (totalDraw, ent.Comp.BaseDraw, partsDraw, modulesDraw);
    }

    private void UpdateUi(Entity<ModSuitComponent> ent)
    {
        var parts = new Dictionary<ModSuitPart, ModSuitPartUiState>();
        foreach (var (part, uid) in ent.Comp.Parts)
            parts[part] = new ModSuitPartUiState(GetNetEntity(uid), IsDeployed(ent, part));

        var modules = new List<ModSuitModuleUiState>();
        foreach (var moduleUid in ent.Comp.ModulesContainer.ContainedEntities)
        {
            var expenditure = 0f;
            var active = true;

            if (TryComp<ModSuitModuleComponent>(moduleUid, out var moduleComp))
            {
                expenditure = moduleComp.Expenditure;
                active = moduleComp.Active;
            }

            modules.Add(new ModSuitModuleUiState(
                GetNetEntity(moduleUid),
                Name(moduleUid),
                expenditure,
                active));
        }

        var (totalDraw, baseDraw, partsDraw, modulesDraw) = CalculateDraw(ent);

        _ui.SetUiState(ent.Owner, ModSuitUiKey.Key, new ModSuitBoundUiState(
            ent.Comp.Wearer is { } wearer ? GetNetEntity(wearer) : null,
            GetCharge(ent),
            totalDraw,
            baseDraw,
            ent.Comp.DrawPerPart,
            modulesDraw,
            ent.Comp.EquippedParts,
            ent.Comp.Parts.Count,
            ent.Comp.Locked,
            GetModSuitStatus(ent),
            parts,
            modules));
    }

    private float? GetCharge(Entity<ModSuitComponent> ent)
    {
        if (!_cell.TryGetBatteryFromSlot(ent.Owner, out var battery) || battery is not { } bat)
            return null;

        return _battery.GetChargeLevel(bat.AsNullable());
    }

    private bool HasCharge(Entity<ModSuitComponent> ent)
    {
        return TryComp<PowerCellDrawComponent>(ent, out var draw)
            && TryComp<PowerCellSlotComponent>(ent, out var slot)
            && _cell.HasDrawCharge((ent.Owner, draw, slot));
    }

    private void UpdateDraw(Entity<ModSuitComponent> ent)
    {
        if (!TryComp<PowerCellDrawComponent>(ent, out var draw))
            return;

        var (totalDraw, _, _, _) = CalculateDraw(ent);

        _cell.SetDrawRate((ent.Owner, draw), totalDraw);
        _cell.SetDrawEnabled((ent.Owner, draw), totalDraw > 0);
    }
}
