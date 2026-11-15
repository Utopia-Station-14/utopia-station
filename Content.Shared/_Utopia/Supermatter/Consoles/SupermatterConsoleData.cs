using Content.Shared.Atmos;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Supermatter.Components;

[Serializable, NetSerializable]
public enum SupermatterConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SupermatterConsoleBoundInterfaceState : BoundUserInterfaceState
{
    public SupermatterConsoleEntry[] Supermatters;
    public SupermatterFocusData? FocusData;

    public SupermatterConsoleBoundInterfaceState(SupermatterConsoleEntry[] supermatters, SupermatterFocusData? focusData)
    {
        Supermatters = supermatters;
        FocusData = focusData;
    }
}

/// <summary>
/// Одна строка в списке-селекторе СМов (центр-низ на макете).
/// </summary>
[Serializable, NetSerializable]
public sealed class SupermatterConsoleEntry
{
    public NetEntity NetEntity;
    public string Location;
    public float Integrity;
    public SupermatterStatus Status;

    public SupermatterConsoleEntry(NetEntity netEntity, string location, float integrity, SupermatterStatus status)
    {
        NetEntity = netEntity;
        Location = location;
        Integrity = integrity;
        Status = status;
    }
}

/// <summary>
/// Развёрнутые данные выбранного (сфокусированного) СМа — для левой панели и газов.
/// </summary>
[Serializable, NetSerializable]
public sealed class SupermatterFocusData
{
    public NetEntity NetEntity;
    public string EntityName;
    public string? Prototype;

    public float Integrity;
    public SupermatterStatus Status;
    public float Temperature;
    public float MinTemperature;
    public float MaxTemperature;

    public float TotalEnergy;
    public float InternalEnergy;
    public float ExternalEnergy;

    public float Radiation;

    public GasMixture GasStorage;
    public float EnergyScaleModifier;
    public float EnergyReductionModifier;
    public float TemperatureScaleModifier;
    public float WasteOutputModifier;
    public float TemperatureProtectionModifier;

    public SupermatterFocusData(
        NetEntity netEntity,
        string entityName,
        string? prototype,
        float integrity,
        SupermatterStatus status,
        float temperature,
        float minTemperature,
        float maxTemperature,
        float totalEnergy,
        float internalEnergy,
        float externalEnergy,
        float radiation,
        GasMixture gasStorage,
        float energyScaleModifier,
        float energyReductionModifier,
        float temperatureScaleModifier,
        float wasteOutputModifier,
        float temperatureProtectionModifier)
    {
        NetEntity = netEntity;
        EntityName = entityName;
        Prototype = prototype;
        Integrity = integrity;
        Status = status;
        Temperature = temperature;
        MinTemperature = minTemperature;
        MaxTemperature = maxTemperature;
        TotalEnergy = totalEnergy;
        InternalEnergy = internalEnergy;
        ExternalEnergy = externalEnergy;
        Radiation = radiation;
        GasStorage = gasStorage;
        EnergyScaleModifier = energyScaleModifier;
        EnergyReductionModifier = energyReductionModifier;
        TemperatureScaleModifier = temperatureScaleModifier;
        WasteOutputModifier = wasteOutputModifier;
        TemperatureProtectionModifier = temperatureProtectionModifier;

    }
}

[Serializable, NetSerializable]
public sealed class SupermatterConsoleFocusChangeMessage : BoundUserInterfaceMessage
{
    public NetEntity? NetEntity;

    public SupermatterConsoleFocusChangeMessage(NetEntity? netEntity)
    {
        NetEntity = netEntity;
    }
}
