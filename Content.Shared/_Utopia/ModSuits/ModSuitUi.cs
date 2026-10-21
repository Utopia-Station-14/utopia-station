using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ModSuits;

[Serializable, NetSerializable]
public sealed class ModSuitPartUiState(NetEntity? entity, bool deployed)
{
    public readonly NetEntity? Entity = entity;
    public readonly bool Deployed = deployed;
}

[Serializable, NetSerializable]
public sealed class ModSuitModuleUiState(
    NetEntity entity,
    string name,
    float expenditure,
    bool active,
    bool enabled = true,
    bool toggleable = false)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly float Expenditure = expenditure;
    public readonly bool Active = active;
    public readonly bool Enabled = enabled;
    public readonly bool Toggleable = toggleable;
}

[Serializable, NetSerializable]
public sealed class ModSuitBoundUiState(
    NetEntity? wearer,
    float? charge,
    float totalDraw,
    float speedDraw,
    float modulesDraw,
    float partSwapCost,
    bool speedEnabled,
    int equippedParts,
    int totalParts,
    bool locked,
    ModSuitStatus status,
    Dictionary<ModSuitPart, ModSuitPartUiState> parts,
    List<ModSuitModuleUiState> modules,
    string? wearerName = null,
    string? wearerJob = null) : BoundUserInterfaceState
{
    public readonly string? WearerName = wearerName;
    public readonly string? WearerJob = wearerJob;
    public readonly NetEntity? Wearer = wearer;
    public readonly float? Charge = charge;
    public readonly float TotalDraw = totalDraw;
    public readonly float SpeedDraw = speedDraw;
    public readonly float ModulesDraw = modulesDraw;
    public readonly float PartSwapCost = partSwapCost;
    public readonly bool SpeedEnabled = speedEnabled;
    public readonly int EquippedParts = equippedParts;
    public readonly int TotalParts = totalParts;
    public readonly bool Locked = locked;
    public readonly ModSuitStatus Status = status;
    public readonly Dictionary<ModSuitPart, ModSuitPartUiState> Parts = parts;
    public readonly List<ModSuitModuleUiState> Modules = modules;
}


[Serializable, NetSerializable]
public sealed class ModSuitTogglePartMessage(ModSuitPart part) : BoundUserInterfaceMessage
{
    public readonly ModSuitPart Part = part;
}

[Serializable, NetSerializable]
public sealed class ModSuitToggleModuleMessage(NetEntity module) : BoundUserInterfaceMessage
{
    public readonly NetEntity Module = module;
}

[Serializable, NetSerializable]
public sealed class ModSuitToggleAllMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ModSuitToggleSpeedMessage : BoundUserInterfaceMessage;
