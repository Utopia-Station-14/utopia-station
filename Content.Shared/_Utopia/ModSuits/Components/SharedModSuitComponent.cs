using Content.Shared.Actions;
using Robust.Shared.Network;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ModSuits;

[Serializable, NetSerializable]
public enum ModSuitPart : byte
{
    Helmet,
    Body,
    Gloves,
    Boots
}

[Serializable, NetSerializable]
public enum ModSuitUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ModSuitPartUiState(NetEntity? entity, bool deployed)
{
    public readonly NetEntity? Entity = entity;
    public readonly bool Deployed = deployed;
}

[Serializable, NetSerializable]
public sealed class ModSuitModuleUiState(NetEntity entity, string name, float expenditure, bool active)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly float Expenditure = expenditure;
    public readonly bool Active = active;
}

[Serializable, NetSerializable]
public sealed class ModSuitBoundUiState(
    NetEntity? wearer,
    float? charge,
    float totalDraw,
    float baseDraw,
    float drawPerPart,
    float modulesDraw,
    int equippedParts,
    int totalParts,
    bool locked,
    ModSuitStatus status,
    Dictionary<ModSuitPart, ModSuitPartUiState> parts,
    List<ModSuitModuleUiState> modules) : BoundUserInterfaceState
{
    public readonly NetEntity? Wearer = wearer;
    public readonly float? Charge = charge;
    public readonly float TotalDraw = totalDraw;
    public readonly float BaseDraw = baseDraw;
    public readonly float DrawPerPart = drawPerPart;
    public readonly float ModulesDraw = modulesDraw;
    public readonly int EquippedParts = equippedParts;
    public readonly int TotalParts = totalParts;
    public readonly bool Locked = locked;
    public readonly ModSuitStatus Status = status;
    public readonly Dictionary<ModSuitPart, ModSuitPartUiState> Parts = parts;
    public readonly List<ModSuitModuleUiState> Modules = modules;
}

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
public sealed class ModSuitTogglePartMessage(ModSuitPart part) : BoundUserInterfaceMessage
{
    public readonly ModSuitPart Part = part;
}


[Serializable, NetSerializable]
public sealed partial class ModSuitPanelDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class ModSuitToggleAllMessage : BoundUserInterfaceMessage;

public sealed partial class ToggleModSuitUiEvent : InstantActionEvent;


[Serializable, NetSerializable]
public enum ModSuitStatus
{
    Inactive,
    Active,
    Charging,
    NeedCharging,
    Dead,
    SPRINGTRAPPED,
}
