using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server._Utopia.Atmos.Components;

[RegisterComponent]
public sealed partial class HeatExchangingPipeComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("node")]
    public string NodeName { get; set; } = "pipe";

    /// <summary>
    /// Минимальная разница температур для теплообмена (K)
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float MinimumTemperatureDifference = 20f;

    /// <summary>
    /// Коэффициент теплопередачи
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float ThermalConductivity = 8000f;

    /// <summary>
    /// Температура начала нанесения урона мобам
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float BurnStart = 1000f;

    /// <summary>
    /// Коэффициент теплового излучения.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public float RadiationCoefficient { get; set; } = 140f;
}