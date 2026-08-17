using Content.Server.Radiation.Systems;

namespace Content.Server.Radiation.Components;

/// <summary>
///     Prevents entities from emitting or receiving radiation when placed inside this container.
/// </summary>
[RegisterComponent]
[Access(typeof(RadiationSystem))]
public sealed partial class RadiationBlockingContainerComponent : Component
{
    // Utopia-Tweak : RadiationUpdt
    /// <summary>
    ///     Flat reduction in radiation when an item is in the container.
    /// </summary>
    [DataField("resistance")]
    public float RadResistance = 0f;

    /// <summary>
    ///     TODO: Clarify this.
    ///     Radiation decay for the Goobstation radiation overhaul after applying the flat reduction.
    /// </summary>
    [DataField("decay")]
    public float RadDecay = 1f;
    // Utopia-Tweak : RadiationUpdt
}
