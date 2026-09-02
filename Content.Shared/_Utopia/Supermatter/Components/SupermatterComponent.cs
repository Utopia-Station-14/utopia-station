using Content.Shared.Atmos;
using Robust.Shared.GameStates;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Supermatter.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SupermatterComponent : Component
{
    #region Base
    /// <summary>
    /// Спит или не спит... вот в чём вопрос.
    /// </summary>
    [DataField]
    public bool Active = false;

    [DataField]
    public SupermatterStatus Status = SupermatterStatus.Inactive;

    /// <summary>
    /// Целостность кристалла Суперматерии.
    /// </summary>
    [DataField]
    public float Integrity = 100f;

    [DataField]
    public bool Delamination = false;

    [DataField]
    public DelaminationType DelaminationType = DelaminationType.Explosion;

    /// <summary>
    /// Текущая температура на тайле с кристаллом Суперматерии.
    /// </summary>
    [DataField]
    public float CurrentTemperature;
    #endregion


    #region Energy
    /// <summary>
    /// Общее количество энергии кристалла Суперматерии.
    /// <seealso cref="InternalEnergy"/>
    /// <seealso cref="ExternalEnergy"/>
    /// </summary>
    [DataField]
    public float TotalEnergy;
    [DataField]
    public float InternalEnergy = 0f;
    [DataField]
    public float ExternalEnergy = 0f;

    /// <summary>
    /// Коэффициент передачи энергии между внутренней и внешней средой (0.0 - 1.0).
    /// </summary>
    [DataField] public float EnergyTransmissionModifier = 0.1f;

    /// <summary>
    /// Коэффициент рассеивания/редукции энергии при нагреве газов и молниях.
    /// </summary>
    [DataField] public float EnergyReductionModifier = 0.05f;

    /// <summary>
    /// Целевое соотношение External / Internal (например, 2.0).
    /// </summary>
    [DataField] public float TargetEnergyRatio = 2.0f;
    #endregion


    #region Radiation
    /// <summary>
    /// Кол-во радиации выделяемое кристаллом Суперматерии.
    /// </summary>
    [DataField]
    public float Radiation = 3f;
    #endregion

    #region Visuals
    /// <summary>
    /// Текущее визуальное состояние кристалла Суперматерии.
    /// </summary>
    [ViewVariables]
    public SupermatterVisualState VisualState = SupermatterVisualState.Inactive;
    #endregion

    #region Gases
    /// <summary>
    /// Максимальная температура, после которой кристалл Суперматерии начинает дестабилизироваться.
    /// </summary>
    [DataField]
    public float MaxTemperature = 500f;

    /// <summary>
    /// Минимальная температура, после которой кристалл Суперматерии начинает дестабилизироваться.
    /// </summary>
    [DataField]
    public float MinTemperature = 73.5f;

    /// <summary>
    /// Список газов контактирующих с кристаллом Суперматерии.
    /// </summary>
    [DataField]
    public GasMixture AtmosGas = new();

    /// <summary>
    /// Список газов, которые являются отходами кристалла.
    /// </summary>
    [DataField]
    public GasMixture WasteGas = new();
    #endregion


    #region Reagents
    /// <summary>
    /// Тут содержатся реагенты, которые попадают в кристалл Суперматерии, при сжигании <see cref="Supermatter.Eating.cs"/>
    /// Участвуют в изменении различных модификаторов на равне с газами! <seealso cref="Supermatter.Reagents.cs"/>
    /// </summary>
    [ViewVariables]
    public Entity<SolutionComponent>? Solution;

    /// <summary>
    /// Название контейнера для хранения реагентов внутри кристалла Суперматерии.
    /// </summary>
    [DataField]
    public string SolutionName = "Supermatter";
    #endregion


    #region Damage
    /// <summary>
    /// Переменная, которая содержит текущее повреждение кристалла Суперматерии.
    /// <see cref="Supermatter.Damage.cs"/>
    /// </summary>
    [DataField]
    public float CurrentDamage;
    [DataField]
    public SupermatterDamageType DamageType;

    /// <summary>
    /// Общее количество повреждений за всё время.
    /// </summary>
    [DataField]
    public float ArchivedDamage;

    [DataField]
    public float HealingModificator = 1f;
    #endregion


    #region TimeSpans
    /// <summary>
    /// Таймер для молний.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextLightningTime;

    /// <summary>
    /// Таймер для оповещений.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextSpeechTime;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan DelaminationEndTime;
    #endregion


    #region Modificators
    [DataField]
    public float BaseModificator = 1f; // TODO: по возможности убрать?

    public float RadiationModificator = 1;

    /// <summary>
    /// Переменная отвечающая за скорость применения модификаторов кристалла.
    /// </summary>
    [DataField]
    public float ModificatorDecayRate = 0.05f;

    /// <summary>
    /// Модификатор нагревания отходных газов кристалла Суперматерии.
    /// </summary>
    [DataField]
    public float TemperatureScaleModificator;

    /// <summary>
    /// Модификатор, участвующий в рассчётах максимальной температуры, <seealso cref="MaxTemperature"/>
    /// </summary>
    [DataField]
    public float TemperatureProtectionModificator;

    /// <summary>
    /// Модификатор, участвующий в рассчёте набора энергии кристаллом Суперматерии <seealso cref="TotalEnergy"/>.
    /// </summary>
    [DataField]
    public float EnergyScaleModificator;

    /// <summary>
    /// Модификатор, участвующий в рассчёте кол-ва молей отходов кристалла Суперматерии <seealso cref="WasteGas"/>
    /// </summary>
    [DataField]
    public float WasteOutputModificator;

    [DataField]
    public float CascadeModificator = 0f;
    #endregion
}

[Serializable, NetSerializable]
public enum SupermatterDamageType : byte
{
    Heat,
    Cool,
    Energy,
    Mole,
    Cascade
}

[Serializable, NetSerializable]
public enum SupermatterStatus : byte
{
    Inactive,
    Stable,
    Warning,
    Destabilization,
    Catastrophe,
    Delamination
}

public enum DelaminationType : byte
{
    Explosion,
    Singularity,
    Tesla,
    Cascade
}


[Serializable, NetSerializable]
public enum SupermatterVisuals : byte
{
    Status
}

[Serializable, NetSerializable]
public enum SupermatterVisualLayers : byte
{
    Base,
    Crystal,
    Glow
}

[Serializable, NetSerializable]
public enum SupermatterVisualState : byte
{
    Inactive,
    Stable,
    Destabilization
}
