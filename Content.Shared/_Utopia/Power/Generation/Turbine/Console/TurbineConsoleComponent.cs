using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Power.Turbines.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TurbineConsoleComponent : Component
{
    /// <summary>
    /// Текущая турбина, выбранная в UI
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public NetEntity? FocusTurbine;

    /// <summary>
    /// Все турбины, видимые консоли (только на сервере)
    /// </summary>
    [ViewVariables]
    public TurbineConsoleEntry[] Turbines = Array.Empty<TurbineConsoleEntry>();

    /// <summary>
    /// Данные выбранной турбины (только на сервере)
    /// </summary>
    [ViewVariables]
    public TurbineFocusData? FocusData;
}

[Serializable, NetSerializable]
public struct TurbineFocusData(
    NetEntity netEntity,
    float currentRpm,
    float maxRpm,
    float maxTemperature,
    float integrity)
{
    public NetEntity NetEntity = netEntity;
    public float CurrentRPM = currentRpm;
    public float MaxRPM = maxRpm;
    public float MaxTemperature = maxTemperature;
    public float Integrity = integrity;
}

[Serializable, NetSerializable]
public sealed class TurbineConsoleBoundInterfaceState(
    TurbineConsoleEntry[] turbines,
    TurbineFocusData? focusData)
    : BoundUserInterfaceState
{
    /// <summary>
    /// Все турбины, видимые консоли
    /// </summary>
    public TurbineConsoleEntry[] Turbines = turbines;

    /// <summary>
    /// Данные выбранной турбины
    /// </summary>
    public TurbineFocusData? FocusData = focusData;
}

[Serializable, NetSerializable]
public struct TurbineConsoleEntry(
    NetEntity entity,
    string entityName,
    TurbineStatusType status)
{
    public NetEntity NetEntity = entity;
    public string EntityName = entityName;
    public TurbineStatusType Status = status;
}

[Serializable, NetSerializable]
public sealed class TurbineConsoleFocusChangeMessage(NetEntity? focusTurbine) : BoundUserInterfaceMessage
{
    public NetEntity? FocusTurbine = focusTurbine;
}

[NetSerializable, Serializable]
public enum TurbineConsoleVisuals
{
    ComputerLayerScreen
}

[Serializable, NetSerializable]
public enum TurbineConsoleUiKey
{
    Key
}

[Serializable, NetSerializable]
public enum TurbineStatusType
{
    Offline,
    Nominal,
    Warning,
    Critical
}