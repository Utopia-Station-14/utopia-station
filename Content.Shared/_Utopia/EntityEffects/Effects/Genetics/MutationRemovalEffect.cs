using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Genetics;

public sealed partial class MutationRemoval : EntityEffectBase<MutationRemoval>
{
    [DataField]
    public float Chance = 1.0f;

    [DataField]
    public int MinRemovals = 1;

    [DataField]
    public int MaxRemovals = 1;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-mutation-removal", ("chance", Probability));
}
