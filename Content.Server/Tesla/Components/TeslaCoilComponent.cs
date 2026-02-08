using Content.Server.Tesla.EntitySystems;

namespace Content.Server.Tesla.Components;

/// <summary>
/// Generates electricity from lightning bolts
/// </summary>
[RegisterComponent, Access(typeof(TeslaCoilSystem))]
public sealed partial class TeslaCoilComponent : Component
{
    // Utopia-Tweak : Lightning-Update
    /// <summary>
    /// Maybe later idk.
    /// </summary>
    [DataField]
    public int ChargingMultiplier = 1;
    // Utopia-Tweak : Lightning-Update
}
