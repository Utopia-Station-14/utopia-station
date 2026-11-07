using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

/// <summary>
///     Снижает распад энергии пропорционально доле газа в смеси.
///     Эффект растёт линейно от MinShare до FullShare, дальше выходит на плато.
/// </summary>
public sealed partial class SupermatterReduceEnergyDecayEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    /// <summary>Минимальная доля газа, при которой эффект начинает работать.</summary>
    [DataField]
    public float MinShare = 0.1f;

    /// <summary>Доля газа, при которой эффект достигает максимума.</summary>
    [DataField]
    public float FullShare = 0.5f;

    /// <summary>Максимальный вклад в EnergyReductionModifier.</summary>
    [DataField]
    public float Strength = 50f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        if (args.TotalMoles <= 0f)
            return;

        var share = args.GasMixture.GetMoles(Gas) / args.TotalMoles;
        if (share < MinShare)
            return;

        var scale = FullShare > MinShare
            ? Math.Clamp((share - MinShare) / (FullShare - MinShare), 0f, 1f)
            : 1f;

        args.Supermatter.Comp.EnergyReductionModifier += scale * Strength;
    }
}
