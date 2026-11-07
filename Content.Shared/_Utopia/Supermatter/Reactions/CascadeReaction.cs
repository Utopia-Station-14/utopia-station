using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

/// <summary>
///     Запускает каскад, если доля HyperGas среди двух газов лежит в заданном окне.
///     Эффект сам определяет условие, поэтому tolerance реакции в YAML лучше сделать широким.
/// </summary>
public sealed partial class SupermatterCascadeEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas HyperGas;

    [DataField(required: true)]
    public Gas AntiGas;

    [DataField]
    public float MinHyperRatio = 0.35f;

    [DataField]
    public float MaxHyperRatio = 0.45f;

    /// <summary>Минимум суммарных молей двух газов, чтобы следы не запускали каскад.</summary>
    [DataField]
    public float MinTotalMoles = 5f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var hyper = args.GasMixture.GetMoles(HyperGas);
        var anti = args.GasMixture.GetMoles(AntiGas);
        var total = hyper + anti;

        if (total < MinTotalMoles)
            return;

        var ratio = hyper / total;
        if (ratio >= MinHyperRatio && ratio <= MaxHyperRatio)
            args.Supermatter.Comp.CascadeModifier = 1f;
    }
}
