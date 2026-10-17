using Content.Server.Radiation.Systems;

namespace Content.Server.Radiation.Components;

/// <summary>
///     Blocks radiation when placed on tile.
/// </summary>
[RegisterComponent]
[Access(typeof(RadiationSystem))]
public sealed partial class RadiationBlockerComponent : Component
{
    /// <summary>
    ///     Does it block radiation at all?
    /// </summary>
    [DataField("enabled")]
    public bool Enabled = true;

    /// <summary>
    ///     How many rads per second does the blocker absorb?
    /// </summary>
    [DataField("resistance")]
    public float RadResistance = 1f;

    // Utopia-Tweak : RadiationUpdt
    [DataField("gammaResistance")]
    public float GammaResistance = 0f;
    // Utopia-Tweak : RadiationUpdt

    /// <summary>
    ///     Current position of the rad blocker in grid coordinates.
    ///     Null if doesn't anchored or doesn't block rads.
    /// </summary>
    public (EntityUid Grid, Vector2i Tile)? CurrentPosition;
}
