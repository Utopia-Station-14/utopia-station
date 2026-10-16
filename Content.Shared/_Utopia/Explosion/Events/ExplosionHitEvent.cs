using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._Utopia.Explosion.Events;

[ByRefEvent]
public record struct ExplosionPowerEvent(
    MapCoordinates Epicenter,
    float Slope,
    float MaxTileIntensity,
    float CurrentIntensity,
    float TotalIntensity
    );
