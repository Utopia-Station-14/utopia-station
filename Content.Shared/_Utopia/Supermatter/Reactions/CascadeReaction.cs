using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

public sealed partial class SupermatterCascadeEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas AntiNoblium;

    [DataField(required: true)]
    public Gas HyperNoblium;

    [DataField]
    public float MinHyperRatio = 0.35f;

    [DataField]
    public float MaxHyperRatio = 0.45f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var gasMixture = args.GasMixture;

        var hyperMoles = gasMixture.GetMoles(HyperNoblium);
        var antiMoles = gasMixture.GetMoles(AntiNoblium);

        var totalTargetMoles = hyperMoles + antiMoles;
        if (totalTargetMoles <= 0f)
            return;

        var hyperRatio = hyperMoles / totalTargetMoles;
        if (hyperRatio >= MinHyperRatio && hyperRatio <= MaxHyperRatio)
            args.Supermatter.Comp.CascadeModifier = 1f;
    }
}
