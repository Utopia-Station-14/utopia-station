using Content.Shared.Atmos;
using Content.Shared._Utopia.Supermatter.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

public sealed partial class SupermatterReduceEnergyDecayEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    private const float MinMoles = 15f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        if (args.TotalMoles < MinMoles)
            return;

        args.Supermatter.Comp.EnergyReductionModifier += args.TotalMoles;
    }
}
