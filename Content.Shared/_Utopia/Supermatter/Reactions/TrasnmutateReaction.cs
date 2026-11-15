using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

/// <summary>
///     Превращает один газ в другой и (опционально) выдаёт энергию.
/// </summary>
public sealed partial class SupermatterTransmuteGasEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas SourceGas;

    [DataField(required: true)]
    public Gas ResultGas;

    [DataField]
    public float EnergyPerMole;

    /// <summary>Сколько молей результата получается из одного моля исходного газа.</summary>
    [DataField]
    public float Modifier = 1f;

    [DataField]
    public float RatePerSecond = 0.2f;

    /// <summary>Лимит за тик. 0 или меньше - без лимита.</summary>
    [DataField]
    public float MaxMolesPerTick = 1f;

    [DataField]
    public float MinPressure;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var mix = args.GasMixture;
        var moles = mix.GetMoles(SourceGas);

        if (moles <= 0f || mix.Pressure < MinPressure)
            return;

        var converted = moles * RatePerSecond * args.FrameTime;

        if (MaxMolesPerTick > 0f)
            converted = MathF.Min(converted, MaxMolesPerTick);

        converted = MathF.Min(converted, moles);

        if (converted <= 0f)
            return;

        mix.AdjustMoles(SourceGas, -converted);
        mix.AdjustMoles(ResultGas, converted * Modifier);
        args.Supermatter.Comp.InternalEnergy += converted * EnergyPerMole;
    }
}
