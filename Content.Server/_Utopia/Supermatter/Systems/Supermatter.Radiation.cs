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

        // Интенсивность радиации равна внешней энергии СМа и парочке модификаторов, покрытыми RadiationOutput.
        var power = (sm.Comp.ExternalEnergy * sm.Comp.RadiationModifier + GetIntegrityModifier(sm)) * RadiationOutput;

        var lerpFactor = 1f - MathF.Exp(-sm.Comp.ModifierDecayRate * frameTime);
        var intensity = MathHelper.Lerp(rad.Intensity, power, lerpFactor);

        ProcessRadiationEnergy(sm, power * frameTime); // Забираем энергию за радиоактивность.

        if (MathHelper.CloseTo(rad.Intensity, intensity, 0.01f))
            return;

        rad.Intensity = intensity;
        rad.Slope = MathHelper.Lerp(rad.Slope, GetRadiationSlope(intensity), lerpFactor);

        sm.Comp.Radiation = rad.Intensity;
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
