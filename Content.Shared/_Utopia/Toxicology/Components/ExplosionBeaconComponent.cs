using Content.Shared.DeviceLinking;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Toxicology.Components;

[RegisterComponent]
public sealed partial class ExplosionBeaconComponent : Component
{
    [DataField]
    public ProtoId<SinkPortPrototype> LinkingPort = "ExplosionBeaconReceiver";

    [DataField]
    public float TargetSlope;

    [DataField]
    public float TargetIntensity;

    [DataField]
    public float TargetCurrentIntensity;

    [DataField]
    public int TargetSlopeMin = 100;

    [DataField]
    public int TargetSlopeMax = 500;

    [DataField]
    public int TargetIntensityMin = 100;

    [DataField]
    public int TargetIntensityMax = 500;

    [DataField]
    public int CurrentAttempt;

    [DataField]
    public int MaxAttempts = 3;

    [DataField]
    public int MinPoints = 10;

    [ViewVariables]
    public float? LastSlope;

    [ViewVariables]
    public float? LastTotalIntensity;

    [ViewVariables]
    public float? LastCurrentIntensity;

    [ViewVariables]
    public int? LastPoints;
}
