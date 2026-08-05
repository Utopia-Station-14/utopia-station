using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Genetics;

public sealed partial class RandomMutation : EntityEffectBase<RandomMutation>
{
    /// <summary>
    /// Chance (0.0–1.0) that a random mutation is triggered each time the effect runs.
    /// </summary>
    [DataField]
    public float Chance = 1.0f;

    /// <summary>
    /// Minimum number of mutations to attempt (will be rolled between Min and Max).
    /// </summary>
    [DataField]
    public int MinMutations = 1;

    /// <summary>
    /// Maximum number of mutations to attempt.
    /// </summary>
    [DataField]
    public int MaxMutations = 1;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-mutation", ("chance", Probability));
}
