namespace Content.Shared._Utopia.ModSuits;

public abstract partial class SharedModSuitSystem
{
    private void InitializeToggle()
    {
        SubscribeLocalEvent<ModSuitPartComponent, ToggleModSuitModuleEvent>(OnToggleModuleAction);
    }

    protected bool TryToggleModule(
        Entity<ModSuitComponent> suit,
        Entity<ModSuitModuleComponent> module,
        out bool enabled)
    {
        enabled = module.Comp.Enabled;

        if (!module.Comp.Toggleable || !suit.Comp.ModulesContainer.Contains(module.Owner))
            return false;

        enabled = !module.Comp.Enabled;
        module.Comp.Enabled = enabled;
        Dirty(module);

        OnModuleToggled(suit);
        return true;
    }

    protected string GetToggleText(EntityUid module, bool enabled)
    {
        return Loc.GetString(
            enabled ? "modsuit-module-enabled" : "modsuit-module-disabled",
            ("name", Name(module)));
    }

    private Entity<ModSuitModuleComponent>? FindModule(Entity<ModSuitComponent> suit, string key, ModSuitPart part)
    {
        foreach (var uid in suit.Comp.ModulesContainer.ContainedEntities)
        {
            if (TryComp<ModSuitModuleComponent>(uid, out var module)
                && module.Key == key
                && module.RequiredPart == part)
            {
                return new Entity<ModSuitModuleComponent>(uid, module);
            }
        }

        return null;
    }

    private void OnToggleModuleAction(Entity<ModSuitPartComponent> ent, ref ToggleModSuitModuleEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryGetSuit(ent, out var suit) || suit.Comp.Wearer != args.Performer)
            return;

        if (suit.Comp.Locked)
        {
            _popup.PopupClient(Loc.GetString("modsuit-locked-no-energy"), args.Performer, args.Performer);
            return;
        }

        if (FindModule(suit, args.Key, ent.Comp.Part) is not { } module)
            return;

        if (TryToggleModule(suit, module, out var enabled))
            _popup.PopupClient(GetToggleText(module.Owner, enabled), args.Performer, args.Performer);
    }
}
