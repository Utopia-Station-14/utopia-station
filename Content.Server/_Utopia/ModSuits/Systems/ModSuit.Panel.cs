using Content.Shared._Utopia.ModSuits;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Verbs;

namespace Content.Server._Utopia.ModSuits.Systems;

public sealed partial class ModSuitSystem
{
    private void OnInteractUsing(Entity<ModSuitComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tool.HasQuality(args.Used, ent.Comp.PanelTool))
        {
            args.Handled = true;

            if (ent.Comp.EquippedParts > 0)
            {
                _popup.PopupEntity(Loc.GetString("modsuit-panel-parts-deployed"), ent, args.User);
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

    private void OnPanelDoAfter(Entity<ModSuitComponent> ent, ref ModSuitPanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.EquippedParts > 0)
            return;

        ent.Comp.PanelOpen = !ent.Comp.PanelOpen;

        var msg = ent.Comp.PanelOpen ? "modsuit-panel-opened" : "modsuit-panel-closed";
        _popup.PopupEntity(Loc.GetString(msg), ent, args.User);

        if (ent.Comp.PanelOpen)
            _ui.CloseUi(ent.Owner, ModSuitUiKey.Key);

        UpdateUi(ent);
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

    private bool TryInstall(Entity<ModSuitComponent> ent, EntityUid user, EntityUid item)
    {
        if (HasComp<ModSuitModuleComponent>(item))
        {
            if (!_container.Insert(item, ent.Comp.ModulesContainer))
                return false;

            _popup.PopupEntity(Loc.GetString("modsuit-module-installed", ("name", Name(item))), ent, user);
            return true;
        }

        if (!TryComp<ClothingComponent>(item, out var clothing) || !TryGetPartType(clothing.Slots, out var part))
            return false;

        if (ent.Comp.Parts.ContainsKey(part))
        {
            _popup.PopupEntity(Loc.GetString("modsuit-part-slot-occupied"), ent, user);
            return true;
        }

        var partComp = EnsureComp<ModSuitPartComponent>(item);
        partComp.Suit = ent;
        partComp.Part = part;
        ent.Comp.Parts[part] = item;

        if (!_container.Insert(item, ent.Comp.PartsContainer))
        {
            ent.Comp.Parts.Remove(part);
            RemComp<ModSuitPartComponent>(item);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("modsuit-part-installed", ("name", Name(item))), ent, user);
        UpdateUi(ent);
        return true;
    }

    private void RemovePart(Entity<ModSuitComponent> ent, EntityUid user, ModSuitPart part)
    {
        if (!ent.Comp.PanelOpen || ent.Comp.EquippedParts > 0)
            return;

        if (!ent.Comp.Parts.TryGetValue(part, out var uid) || !ent.Comp.PartsContainer.Contains(uid))
            return;

        if (!_container.Remove(uid, ent.Comp.PartsContainer))
            return;

        ent.Comp.Parts.Remove(part);
        _pendingReturn.Remove(uid);

        RemComp<ModSuitPartComponent>(uid);
        _hands.PickupOrDrop(user, uid);
        UpdateUi(ent);
    }

    private void RemoveModule(Entity<ModSuitComponent> ent, EntityUid user, EntityUid module)
    {
        if (!ent.Comp.PanelOpen || !ent.Comp.ModulesContainer.Contains(module))
            return;

        if (!_container.Remove(module, ent.Comp.ModulesContainer))
            return;

        _hands.PickupOrDrop(user, module);
    }

    private static bool TryGetPartType(SlotFlags flags, out ModSuitPart part)
    {
        if ((flags & SlotFlags.HEAD) != 0)
            part = ModSuitPart.Helmet;
        else if ((flags & SlotFlags.OUTERCLOTHING) != 0)
            part = ModSuitPart.Body;
        else if ((flags & SlotFlags.GLOVES) != 0)
            part = ModSuitPart.Gloves;
        else if ((flags & SlotFlags.FEET) != 0)
            part = ModSuitPart.Boots;
        else
        {
            part = default;
            return false;
        }

        return true;
    }

    private bool IsPanelOpen(Entity<ModSuitComponent> ent, EntityUid wearer)
    {
        if (!ent.Comp.PanelOpen)
            return false;

        _popup.PopupEntity(Loc.GetString("modsuit-panel-open-blocked"), wearer, wearer);
        return true;
    }
}
