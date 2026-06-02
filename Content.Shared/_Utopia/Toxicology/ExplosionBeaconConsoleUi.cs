using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Toxicology;

[Serializable, NetSerializable]
public enum ExplosionBeaconConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ExplosionBeaconConsoleState : BoundUserInterfaceState
{
    public readonly bool Linked;
    public readonly float TargetSlope;
    public readonly float TargetIntensity;
    public readonly float TargetCurrentIntensity;
    public readonly int CurrentAttempt;
    public readonly int MaxAttempts;
    public readonly float? LastSlope;
    public readonly float? LastTotalIntensity;
    public readonly float? LastCurrentIntensity;
    public readonly int? LastPoints;

    public ExplosionBeaconConsoleState(
        bool linked,
        float targetSlope,
        float targetIntensity,
        float targetCurrentIntensity,
        int currentAttempt,
        int maxAttempts,
        float? lastSlope,
        float? lastTotalIntensity,
        float? lastCurrentIntensity,
        int? lastPoints)
    {
        Linked = linked;
        TargetSlope = targetSlope;
        TargetIntensity = targetIntensity;
        TargetCurrentIntensity = targetCurrentIntensity;
        CurrentAttempt = currentAttempt;
        MaxAttempts = maxAttempts;
        LastSlope = lastSlope;
        LastTotalIntensity = lastTotalIntensity;
        LastCurrentIntensity = lastCurrentIntensity;
        LastPoints = lastPoints;
    }

    public static ExplosionBeaconConsoleState Unlinked { get; } = new(
        linked: false,
        targetSlope: 0,
        targetIntensity: 0,
        targetCurrentIntensity: 0,
        currentAttempt: 0,
        maxAttempts: 0,
        lastSlope: null,
        lastTotalIntensity: null,
        lastCurrentIntensity: null,
        lastPoints: null);
}
