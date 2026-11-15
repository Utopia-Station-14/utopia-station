using Content.Shared.Atmos;

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

    [DataField(required: true)]
    public float MinMoles;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var mix = args.GasMixture;
        var moles = mix.GetMoles(Gas);

        if (moles < MinMoles)
            return;

        var comp = args.Supermatter.Comp;

        comp.LightningCountModifier = Math.Max(comp.LightningCountModifier, Count);
        comp.LightningRangeModifier = MathF.Max(comp.LightningRangeModifier, Range);

        comp.LightningCooldownModifier = comp.LightningCooldownModifier <= 0f
            ? Cooldown
            : MathF.Min(comp.LightningCooldownModifier, Cooldown);
    }
}
