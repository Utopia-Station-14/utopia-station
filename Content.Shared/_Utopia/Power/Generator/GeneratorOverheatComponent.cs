using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Power.Generator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneratorOverheatComponent : Component
{
    /// <summary>
    /// Текущая температура генератора. При инциализации компонента равна 20 по цельсию.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentTemperature = 293.15f;

    /// <summary>
    /// Рабочая температура генератора при среднем значении выработки электропитания.
    /// </summary>
    [DataField]
    public float OperatingTemperature =  373.15f;

    /// <summary>
    /// Критическая температура, после которой происходят последствия.
    /// </summary>
    [DataField]
    public float CriticalTemperature = 423.15f;

    /// <summary>
    /// Скорость нагрева при выработке выше средней.
    /// </summary>
    [DataField]
    public float HeatRatePerKw = 0.1f;

    /// <summary>
    /// Скорость нагрева при средней выработке электропитания.
    /// </summary>
    [DataField]
    public float BaseHeatRate = 0.05f;

    /// <summary>
    /// Немного физики для охлаждения/нагрева генератора и окружающей среды.
    /// </summary>
    [DataField]
    public float HeatCapacity = 20000f;

    [DataField]
    public float ThermalConductance = 200f;

    /// <summary>
    /// Шанс на взрыв генератора при достижении критического значения температуры.
    /// Если вам не нужно, чтобы генератор взрывался - ставьте 0.
    /// В ином случае, чем выше значение данной перменной - тем меньше шанс на взрыв.
    /// </summary>
    [DataField]
    public int ExplodeChance;

    /// <summary>
    /// Флажок для отслеживания перегревов.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CriticalTriggered;
}
