using Content.Shared._Utopia.ModSuits;

namespace Content.Server._Utopia.ModSuits.Systems;
public sealed partial class ModSuitSystem
{
    private void InitializePanelDoAfter()
    {
        SubscribeLocalEvent<ModSuitComponent, ModSuitPanelDoAfterEvent>(OnPanelDoAfter);
    }

    private void OnPanelDoAfter(Entity<ModSuitComponent> ent, ref ModSuitPanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.EquippedParts > 0)
            return;

        ent.Comp.PanelOpen = !ent.Comp.PanelOpen;
        Dirty(ent);

        var msg = ent.Comp.PanelOpen ? "modsuit-panel-opened" : "modsuit-panel-closed";
        _popup.PopupEntity(Loc.GetString(msg), ent, args.User);

        if (ent.Comp.PanelOpen)
            _ui.CloseUi(ent.Owner, ModSuitUiKey.Key);

        UpdateUi(ent);
    }

    private bool IsPanelOpen(Entity<ModSuitComponent> ent, EntityUid wearer)
    {
        if (!ent.Comp.PanelOpen)
            return false;

        _popup.PopupEntity(Loc.GetString("modsuit-panel-open-blocked"), wearer, wearer);
        return true;
    }
}
