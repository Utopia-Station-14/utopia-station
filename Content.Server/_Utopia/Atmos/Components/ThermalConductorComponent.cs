namespace Content.Server.Atmos.Components;

[RegisterComponent]
public sealed partial class ThermalConductorComponent : Component
{
[DataField]
    public float ThermalConductivity = 0.04f;

    [DataField]
    public float? HeatCapacity;

    [DataField]
    public float WallReferenceHeatCapacity = 10000f;

    [DataField]
    public float WallWarmupMinFactor = 0.1f;

    [DataField]
    public float WallWarmupMaxFactor = 10f;
}
