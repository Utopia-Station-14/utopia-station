using System.Collections.Generic;
using Content.Shared._Utopia.Supermatter.Prototypes;
using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Events;

[ByRefEvent]
public readonly record struct SupermatterGasReactionEvent(
    SupermatterReactionPrototype Reaction,
    GasMixture GasMixture,
    float FrameTime
);

[ByRefEvent]
public readonly record struct SupermatterReagentReactionEvent(
    SupermatterReagentReactionPrototype Reaction,
    Dictionary<string, float> CurrentRatios,
    float FrameTime
);
