namespace Content.Server._Utopia.Toxicology.Components;

[RegisterComponent]
public sealed class ExplosionBeaconComponent : Component
{
    /// <summary>
    /// Ниже представленные параметры взрыва, к которым учёный должен приблизиться, чтобы получить больше очков.
    /// </summary>
    [DataField]
    public float TargetSlope;

    [DataField]
    public float TargetIntensity;

    [DataField]
    public float TargetCurrentIntensity;


    /// <summary>
    /// Счётчик неуспешных попыток.
    /// </summary>
    [DataField]
    public int CurrentAttempt = 0;

    /// <summary>
    /// Максимально число неуспешных попыток, которые можно допуситить до перекрутки параметров.
    /// </summary>
    [DataField]
    public int MaxAttempts = 3; 

    /// <summary>
    /// Минимальное кол-во очков, которое должен получить игрок со взрыва, чтобы попытка считалась успешной.
    /// </summary>
    [DataField]
    public int MinPoints = 10;
}