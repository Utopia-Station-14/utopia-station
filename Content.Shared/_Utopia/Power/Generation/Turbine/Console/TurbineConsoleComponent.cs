using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Power.Turbines.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TurbineConsoleComponent : Component
{
    /// <summary>
    /// Текущая турбина, выбранная в UI.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public NetEntity? FocusTurbine;

    /// <summary>
    /// Все турбины, видимые консоли.
    /// </summary>
    [ViewVariables]
    public TurbineConsoleEntry[] Turbines = Array.Empty<TurbineConsoleEntry>();

    /// <summary>
    /// Данные выбранной турбины.
    /// </summary>
    [ViewVariables]
    public TurbineFocusData? FocusData;
}

[Serializable, NetSerializable]
public struct TurbineFocusData(
    NetEntity netEntity,
    TurbineStatusType status,
    float currentRpm,
    float maxRpm,
    float currentPressure,
    float maxPressure,
    float currentTemperature,
    float maxTemperature,
    float integrity,
    float energy,
    bool isActive)
{
    public NetEntity NetEntity = netEntity;
    public TurbineStatusType Status = status;
    public float CurrentRPM = currentRpm;
    public float MaxRPM = maxRpm;
    public float CurrentPressure = currentPressure;
    public float MaxPressure = maxPressure;
    public float CurrentTemperature = currentTemperature;
    public float MaxTemperature = maxTemperature;
    public float Integrity = integrity;
    public float Energy = energy;
    public bool IsActive = isActive;
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

/// <summary>
/// Сообщение, отправляемое клиентом при переключении активности турбины.
/// </summary>
[Serializable, NetSerializable]
public sealed class TurbineConsoleToggleMessage(NetEntity turbine, bool isActive) : BoundUserInterfaceMessage
{
    public NetEntity Turbine = turbine;
    public bool IsActive = isActive;
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
    Off,
    Nominal,
    Warning,
    Critical
}
