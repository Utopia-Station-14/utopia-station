using Content.Shared._Utopia.ModSuits;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private const int MaxRefreshPasses = 8;

    private bool _refreshing;
    private readonly HashSet<EntityUid> _refreshQueue = new();
    private readonly List<EntityUid> _queueBuffer = new();

    private readonly record struct SuitDraw(float Total, float Modules, float Speed);

    private void OnCellEmpty(Entity<ModSuitComponent> ent, ref PowerCellSlotEmptyEvent args)
        => LockSuit(ent);

    private void LockSuit(Entity<ModSuitComponent> ent)
    {
        if (ent.Comp.EquippedParts == 0 || ent.Comp.Locked)
            return;

        ent.Comp.Locked = true;
        Dirty(ent);

        if (ent.Comp.Wearer is { } wearer)
            _popup.PopupEntity(Loc.GetString("modsuit-energy-depleted-locked"), wearer, wearer);

        RefreshSpeed(ent);
        UpdateDraw(ent);
        UpdateUi(ent);
    }

    #region Draw

    private SuitDraw CalculateDraw(Entity<ModSuitComponent> ent)
    {
        if (ent.Comp.EquippedParts <= 0 || ent.Comp.Locked)
            return default;

        var modules = 0f;
        foreach (var uid in ent.Comp.ModulesContainer.ContainedEntities)
        {
            if (TryComp<ModSuitModuleComponent>(uid, out var module) && module.Active)
                modules += module.Expenditure;
        }

        var speed = IsSpeedActive(ent) ? ent.Comp.SpeedDraw : 0f;
        return new SuitDraw(modules + speed, modules, speed);
    }

    private void UpdateDraw(Entity<ModSuitComponent> ent)
    {
        if (_refreshing)
        {
            _refreshQueue.Add(ent.Owner);
            return;
        }

        _refreshing = true;

        try
        {
            RefreshModules(ent);
            ApplyDraw(ent);

            for (var pass = 0; _refreshQueue.Count > 0 && pass < MaxRefreshPasses; pass++)
            {
                _queueBuffer.Clear();
                _queueBuffer.AddRange(_refreshQueue);
                _refreshQueue.Clear();

                foreach (var uid in _queueBuffer)
                {
                    if (TerminatingOrDeleted(uid) || !TryComp<ModSuitComponent>(uid, out var comp))
                        continue;

                    var queued = new Entity<ModSuitComponent>(uid, comp);
                    RefreshModules(queued);
                    ApplyDraw(queued);
                }
            }

            if (_refreshQueue.Count > 0)
            {
                Log.Error("ModSuit: module refresh did not settle, dropping pending refreshes.");
                _refreshQueue.Clear();
            }
        }
        finally
        {
            _refreshing = false;
            _queueBuffer.Clear();
        }
    }

    private void ApplyDraw(Entity<ModSuitComponent> ent)
    {
        if (!TryComp<PowerCellDrawComponent>(ent, out var draw))
            return;

        var total = CalculateDraw(ent).Total;

        _cell.SetDrawRate((ent.Owner, draw), total);
        _cell.SetDrawEnabled((ent.Owner, draw), total > 0f);
    }

    #endregion

    #region Charge

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

    #endregion
}
