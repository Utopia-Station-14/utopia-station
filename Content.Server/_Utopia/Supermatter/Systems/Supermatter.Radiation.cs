using Content.Shared.Radiation.Components;
using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

/// <summary>
/// Обработка радиации СМа.
/// </summary>
public sealed partial class SupermatterSystem
{
    public void ProcessRadiation(Entity<SupermatterComponent> sm, float frameTime)
    {
        if (!TryComp<RadiationSourceComponent>(sm, out var rad))
            return;

        var gammaTarget = sm.Comp.ExternalEnergy * sm.Comp.RadiationModifier * RadiationOutput * GammaRatio * sm.Comp.GammaModifier;
        var betaTarget = (GetIntegrityModifier(sm) + sm.Comp.ExternalEnergy * sm.Comp.RadiationModifier * BetaEnergyShare)
            * RadiationOutput * BetaRatio * sm.Comp.BetaModifier;

        var gammaLerp = 1f - MathF.Exp(-sm.Comp.ModifierDecayRate * GammaInertia * frameTime);
        var betaLerp = 1f - MathF.Exp(-sm.Comp.ModifierDecayRate * BetaInertia * frameTime);

        var gamma = MathHelper.Lerp(rad.GammaIntensity, gammaTarget, gammaLerp);
        var beta = MathHelper.Lerp(rad.Intensity, betaTarget, betaLerp);

        ProcessRadiationEnergy(sm, (gammaTarget + betaTarget) * frameTime);

        if (MathHelper.CloseTo(rad.GammaIntensity, gamma, 0.01f) && MathHelper.CloseTo(rad.Intensity, beta, 0.01f))
            return;

        rad.GammaIntensity = gamma;
        rad.Intensity = beta;
        var totalRadiation = gamma + beta;

        rad.Slope = GetRadiationSlope(rad.GammaIntensity);

        sm.Comp.Radiation = totalRadiation;
        rad.Slope = GetRadiationSlope(totalRadiation);
    }

    /// <summary>
    /// TODO: Сделать метод более полезным?
    /// </summary>
    private static float GetRadiationSlope(float intensity)
    {
        return intensity switch
        {
            >= 20f => 1f,
            >= 10f => 0.5f,
            _ => 0.2f
        };
    }
}
