using Content.Shared.Atmos;
using Content.Shared._Utopia.Supermatter.Prototypes;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

public sealed partial class SupermatterUpgradeLightningEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    [DataField(required: true)]
    public int Count;

    [DataField(required: true)]
    public float Cooldown;

    [DataField(required: true)]
    public float Range;
    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        args.Supermatter.Comp.LightningCountModifier = Count;
        args.Supermatter.Comp.LightningCooldownModifier = Cooldown;
        args.Supermatter.Comp.LightningRangeModifier = Range;
    }
}
