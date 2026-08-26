using Content.Shared.Radiation.Components;
using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    public void ProcessRadiation(Entity<SupermatterComponent> sm)
    {
        if (TryComp<RadiationSourceComponent>(sm, out var rad))
        {
            var currentRadIntensity = sm.Comp.Radiation;
            currentRadIntensity += currentRadIntensity + sm.Comp.ExternalEnergy * sm.Comp.RadiationModificator;

            rad.Intensity = MathHelper.Lerp(rad.Intensity, currentRadIntensity, sm.Comp.ModificatorDecayRate);
            rad.Slope = MathHelper.Lerp(rad.Slope, GetRadiationSlope(currentRadIntensity), sm.Comp.ModificatorDecayRate);
        }
    }

    private float GetRadiationSlope(float intensity)
    {
        var slope = intensity switch
        {
            _ when intensity >= 20f => 1f,
            _ when intensity >= 10f => 0.5f,
            _ => 0.2f
        };
        return slope;
    }
}
