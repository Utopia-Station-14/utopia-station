using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Power.Generator;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneratorRadiationComponent : Component
{
    /// <summary>
    /// Флажок для проверки, не фонит ли генератор уже.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active = false;

    /// <summary>
    /// Модификатор радиации. Для суперпакмана равен += 4.
    /// </summary>
    [DataField]
    public float RadiationMultiplier = 1f;

    /// <summary>
    /// Кол-во уходящей радиации за секунду.
    /// </summary>
    [DataField]
    public float RadiationReduceAmount = 0.5f;
}

public enum GeneratorVisualState
{
    Idle,
    Running,
    Radiating
}