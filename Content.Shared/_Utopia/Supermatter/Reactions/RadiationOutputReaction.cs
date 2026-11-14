using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

/// <summary>
///     Сжигает газ, добавляя бонус к гамма- или бета-излучению и немного энергии.
///     Бонус считается как интенсивность (в секунду), а не как разовая добавка за тик,
///     поэтому не зависит от частоты тиков. GammaModifier/BetaModifier должны
///     сбрасываться в начале тика и подмешиваться в целевую интенсивность в ProcessRadiation.
/// </summary>
public sealed partial class SupermatterRadiationOutputEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    [DataField]
    public bool IsGamma;

    [DataField]
    public float RatePerSecond = 0.1f;

    [DataField]
    public float RadiationPerMole = 20f;

    [DataField]
    public float EnergyPerMole = 3f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var moles = args.GasMixture.GetMoles(Gas);
        if (moles <= 0f || args.FrameTime <= 0f)
            return;

        var burned = MathF.Min(moles, moles * RatePerSecond * args.FrameTime);
        if (burned <= 0f)
            return;

        args.GasMixture.AdjustMoles(Gas, -burned);

        var comp = args.Supermatter.Comp;
        comp.InternalEnergy += burned * EnergyPerMole;

        var bonus = burned * RadiationPerMole / args.FrameTime;

        if (IsGamma)
            comp.GammaModifier += bonus;
        else
            comp.BetaModifier += bonus;
    }
}
