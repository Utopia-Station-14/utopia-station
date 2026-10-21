using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ModSuits;
public sealed partial class ToggleModSuitUiEvent : InstantActionEvent;
public sealed partial class ToggleModSuitModuleEvent : InstantActionEvent
{
    [DataField]
    public string Key = string.Empty;
}


[Serializable, NetSerializable]
public sealed partial class ModSuitPanelDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ModSuitDeployDoAfterEvent : DoAfterEvent
{
    [DataField]
    public ModSuitPart Part;

    [DataField]
    public bool Chain;

    private ModSuitDeployDoAfterEvent()
    {
    }

    public ModSuitDeployDoAfterEvent(ModSuitPart part, bool chain)
    {
        Part = part;
        Chain = chain;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class ModSuitManualRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public ModSuitPart Part;

    private ModSuitManualRemoveDoAfterEvent()
    {
    }

    public ModSuitManualRemoveDoAfterEvent(ModSuitPart part)
    {
        Part = part;
    }

    public override DoAfterEvent Clone() => this;
}


[ByRefEvent]
public readonly record struct ModSuitModuleActivatedEvent(
    Entity<ModSuitComponent> Suit,
    EntityUid? Wearer,
    EntityUid? Part);

[ByRefEvent]
public readonly record struct ModSuitModuleDeactivatedEvent(
    Entity<ModSuitComponent> Suit,
    EntityUid? Wearer,
    EntityUid? Part);

