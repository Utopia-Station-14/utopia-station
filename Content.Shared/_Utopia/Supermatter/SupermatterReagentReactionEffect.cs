using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Chemistry.Components;
using JetBrains.Annotations;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class SupermatterReagentReactionEffect
{
    public abstract void Effect(SupermatterReagentReactionEffectArgs args);
}
public readonly struct SupermatterReagentReactionEffectArgs
{
    public readonly Entity<SupermatterComponent> Supermatter;
    public readonly Solution Solution = null!;
    public readonly Dictionary<string, float> CurrentRatios = null!;
    public readonly float FrameTime;
    public readonly IEntityManager EntityManager = null!;

    public SupermatterReagentReactionEffectArgs(
        Entity<SupermatterComponent> supermatter,
        Solution solution,
        Dictionary<string, float> currentRatios,
        float frameTime,
        IEntityManager entityManager)
    {
        Supermatter = supermatter;
        Solution = solution;
        CurrentRatios = currentRatios;
        FrameTime = frameTime;
        EntityManager = entityManager;
    }
}
