using Content.Shared.Atmos;
using Content.Shared._Utopia.Supermatter.Components;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class SupermatterGasReactionEffect
{
    public abstract void Effect(SupermatterGasReactionEffectArgs args);
}

public readonly struct SupermatterGasReactionEffectArgs
{
    public readonly Entity<SupermatterComponent> Supermatter;
    public readonly GasMixture GasMixture = null!;
    public readonly float FrameTime;
    public readonly float TotalMoles;
    public readonly IEntityManager EntityManager = null!;

    public SupermatterGasReactionEffectArgs(
        Entity<SupermatterComponent> supermatter,
        GasMixture gasMixture,
        float frameTime,
        float totalMoles,
        IEntityManager entityManager)
    {
        Supermatter = supermatter;
        GasMixture = gasMixture;
        FrameTime = frameTime;
        TotalMoles = totalMoles;
        EntityManager = entityManager;
    }
}
