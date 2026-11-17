using Content.Shared.Radiation.Systems;

namespace Content.Shared.Radiation.Components;

/// <summary>
/// Irradiate all objects in range.
/// </summary>
[RegisterComponent]
// [Access(typeof(SharedRadiationSystem))] // Utopia-Tweak : Radiation
public sealed partial class RadiationSourceComponent : Component
{
    // Utopia-Tweak : RadiationUpdt
    /// <summary>
    /// Radiation intensity in center of the source in rads per second.
    /// From there radiation rays will travel over distance and loose intensity
    /// when hit radiation blocker.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Intensity = 1;

    /// <summary>
    /// Defines how fast radiation rays will loose intensity
    /// over distance. The bigger the value, the shorter range
    /// of radiation source will be.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Slope = 0.5f;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;
    // Utopia-Tweak : RadiationUpdt

    /// <summary>
    ///     Defines how fast radiation rays will loose intensity
    ///     over distance if the ray enters terminal decay. The bigger the value, faster the radiation source
    ///     will decay past the TerminalDecayDistance.
    /// </summary>
    [DataField]
    public float TerminalDecaySlope = 0.07f;

    /// <summary>
    ///     Defines distance from source until a radiation ray enters terminal decay.
    ///     Increasing the value increases the distance the the ray will operate under pure hyperbolic decay.
    ///     Hyperbolic decay is horizontially asymptotic at y=0. Terminal decay is an additional
    ///     linear decrement.
    /// </summary>
    [DataField]
    public float TerminalDecayDistance = 15;

    [DataField]
    public float GammaIntensity = 0f;

    [DataField]
    public float GammaTerminalDecaySlope = 0.02f;

    [DataField]
    public float GammaTerminalDecayDistance = 25f;
    // Utopia-Tweak : RadiationUpdt
}
