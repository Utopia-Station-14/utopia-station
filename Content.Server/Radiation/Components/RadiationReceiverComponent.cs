using Content.Server.Radiation.Systems;
using Content.Shared.Radiation.Components;
using Content.Server._Utopia.Radiation.Systems;

namespace Content.Server.Radiation.Components;

/// <summary>
///     Marks component that receive radiation from <see cref="RadiationSourceComponent"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(RadiationSystem), typeof(GammaRadiationSystem))] // Utopia-Tweak : RadiationUpdt
public sealed partial class RadiationReceiverComponent : Component
{
    // Utopia-Tweak : RadiationUpdt
    /// <summary>
    ///     Current Beta radiation value in rads per second.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float BetaRadiation;

    /// <summary>
    ///     Current Gamma radiation value in rads per second.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public float GammaRadiation;
    // Utopia-Tweak : RadiationUpdt
}

