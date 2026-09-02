using Content.Shared.Atmos;
using Content.Shared._Utopia.Supermatter.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

public sealed partial class SupermatterReduceEnergyDecayEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {

    }
}
