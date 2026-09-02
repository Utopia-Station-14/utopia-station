using Content.Shared.Radiation.Components;
using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    public void ProcessRadiation(Entity<SupermatterComponent> sm, float frameTime)
    {
        if (!TryComp<RadiationSourceComponent>(sm, out var rad))
            return;

        var power = (sm.Comp.ExternalEnergy * sm.Comp.RadiationModificator);
        var targetIntensity = rad.Intensity + power;

        var lerpFactor = 1f - MathF.Exp(-sm.Comp.ModificatorDecayRate * frameTime);

        var intensity = MathHelper.Lerp(rad.Intensity, targetIntensity, lerpFactor);
        var slope = GetRadiationSlope(intensity);

        if (MathHelper.CloseTo(rad.Intensity, intensity, 0.01f))
            return;

        rad.Intensity = intensity;
        rad.Slope = MathHelper.Lerp(rad.Slope, slope, lerpFactor);

        ConsumeInternalEnergy(sm, power);
    }

    private float GetRadiationSlope(float intensity)
    {
        return intensity switch
        {
            >= 20f => 1f,
            >= 10f => 0.5f,
            _ => 0.2f
        };
    }
}
