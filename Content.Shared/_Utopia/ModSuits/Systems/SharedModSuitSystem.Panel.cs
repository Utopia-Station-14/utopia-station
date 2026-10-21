using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;

namespace Content.Shared._Utopia.ModSuits;

public abstract partial class SharedModSuitSystem
{
    private void InitializePanel()
    {
        SubscribeLocalEvent<ModSuitComponent, InteractUsingEvent>(
            OnInteractUsing,
            before: new[] { typeof(SharedStorageSystem) });

        SubscribeLocalEvent<ModSuitComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ModSuitComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnInteractUsing(Entity<ModSuitComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, ent.Comp.PanelTool))
        {
            args.Handled = true;

            if (ent.Comp.EquippedParts > 0)
            {
                _popup.PopupClient(Loc.GetString("modsuit-panel-parts-deployed"), ent, args.User);
                return;
            }

            _tool.UseTool(
                args.Used,
                args.User,
                ent,
                ent.Comp.PanelDelay,
                ent.Comp.PanelTool,
                new ModSuitPanelDoAfterEvent());
            return;
        }

        if (!ent.Comp.PanelOpen)
            return;

        args.Handled = TryInstall(ent, args.User, args.Used);
    }

    private void OnExamined(Entity<ModSuitComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.PanelOpen
            ? "modsuit-examine-panel-open"
            : "modsuit-examine-panel-closed"));
    }

    private void OnGetVerbs(Entity<ModSuitComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!ent.Comp.PanelOpen || !args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;

        foreach (var (part, uid) in ent.Comp.Parts)
        {
            if (!ent.Comp.PartsContainer.Contains(uid))
                continue;

            var partType = part;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("modsuit-verb-remove", ("name", Name(uid))),
                Category = VerbCategory.Eject,
                Priority = 1,
                Act = () => RemovePart(ent, user, partType),
            });
        }

        foreach (var module in ent.Comp.ModulesContainer.ContainedEntities)
        {
            var moduleUid = module;
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("modsuit-verb-remove", ("name", Name(moduleUid))),
                Category = VerbCategory.Eject,
                Priority = 0,
                Act = () => RemoveModule(ent, user, moduleUid),
            });
        }
    }

    #region Install / Remove

    private bool TryInstall(Entity<ModSuitComponent> ent, EntityUid user, EntityUid item)
    {
        if (HasComp<ModSuitModuleComponent>(item))
            return TryInstallModule(ent, user, item);

        return TryInstallPart(ent, user, item);
    }

    private bool TryInstallModule(Entity<ModSuitComponent> ent, EntityUid user, EntityUid module)
    {
        if (!_container.Insert(module, ent.Comp.ModulesContainer))
            return false;

        _popup.PopupClient(Loc.GetString("modsuit-module-installed", ("name", Name(module))), ent, user);
        return true;
    }

    private bool TryInstallPart(Entity<ModSuitComponent> ent, EntityUid user, EntityUid item)
    {
        if (!TryComp<ClothingComponent>(item, out var clothing) || !ModSuitSlots.TryGetPart(clothing.Slots, out var part))
            return false;

        if (ent.Comp.Parts.ContainsKey(part))
        {
            _popup.PopupClient(Loc.GetString("modsuit-part-slot-occupied"), ent, user);
            return true;
        }

        var partComp = EnsureComp<ModSuitPartComponent>(item);
        partComp.Suit = ent;
        partComp.Part = part;
        Dirty(item, partComp);

        ent.Comp.Parts[part] = item;
        Dirty(ent.Owner, ent.Comp);

        if (!_container.Insert(item, ent.Comp.PartsContainer))
        {
            ent.Comp.Parts.Remove(part);
            Dirty(ent.Owner, ent.Comp);
            RemComp<ModSuitPartComponent>(item);
            return false;
        }

        _popup.PopupClient(Loc.GetString("modsuit-part-installed", ("name", Name(item))), ent, user);
        UpdateUi(ent);
        return true;
    }

    protected void RemovePart(Entity<ModSuitComponent> ent, EntityUid user, ModSuitPart part)
    {
        if (!ent.Comp.PanelOpen || ent.Comp.EquippedParts > 0)
            return;

        if (!ent.Comp.Parts.TryGetValue(part, out var uid) || !ent.Comp.PartsContainer.Contains(uid))
            return;

        if (!_container.Remove(uid, ent.Comp.PartsContainer))
            return;

        ent.Comp.Parts.Remove(part);
        Dirty(ent.Owner, ent.Comp);
        OnPartDetached(uid);

        RemComp<ModSuitPartComponent>(uid);
        _hands.PickupOrDrop(user, uid);
        UpdateUi(ent);
    }

    protected void RemoveModule(Entity<ModSuitComponent> ent, EntityUid user, EntityUid module)
    {
        if (!ent.Comp.PanelOpen || !ent.Comp.ModulesContainer.Contains(module))
            return;

        if (!_container.Remove(module, ent.Comp.ModulesContainer))
            return;

        _hands.PickupOrDrop(user, module);
    }

    #endregion
}
