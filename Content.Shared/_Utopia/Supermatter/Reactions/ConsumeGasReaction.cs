using Content.Shared.Atmos;
using Content.Shared._Utopia.Supermatter.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

public sealed partial class SupermatterConsumeGasEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    [DataField]
    public float AbsorbRatePerSecond = 0.05f;

    [DataField]
    public float EnergyPerMole;

    [DataField]
    public float MaxMolesPerTick;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var currentMoles = args.GasMixture.GetMoles(Gas);
        if (currentMoles <= 0f)
            return;

        var absorbed = currentMoles * AbsorbRatePerSecond * args.FrameTime;

        if (MaxMolesPerTick > 0f && absorbed > MaxMolesPerTick)
            absorbed = MaxMolesPerTick;

        if (absorbed > currentMoles)
            absorbed = currentMoles;

        if (absorbed <= 0f)
            return;

        args.GasMixture.AdjustMoles(Gas, -absorbed);
        args.Supermatter.Comp.InternalEnergy += absorbed * EnergyPerMole;
    }
}
