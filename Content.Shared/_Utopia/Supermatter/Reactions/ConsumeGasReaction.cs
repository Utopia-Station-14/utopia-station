using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

/// <summary>
///     Поглощает газ и превращает его в внутреннюю энергию.
/// </summary>
public sealed partial class SupermatterConsumeGasEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    [DataField]
    public float AbsorbRatePerSecond = 0.1f;

    [DataField]
    public float EnergyPerMole;

    /// <summary>Лимит за тик. 0 или меньше - без лимита.</summary>
    [DataField]
    public float MaxMolesPerTick;

    /// <summary>Ниже этого количества газ добирается целиком, чтобы не тянуться к нулю вечно.</summary>
    [DataField]
    public float DustThreshold = 0.001f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var currentMoles = args.GasMixture.GetMoles(Gas);
        if (currentMoles <= 0f)
            return;

        var absorbed = currentMoles < DustThreshold
            ? currentMoles
            : currentMoles * AbsorbRatePerSecond * args.FrameTime;

        if (MaxMolesPerTick > 0f)
            absorbed = MathF.Min(absorbed, MaxMolesPerTick);

        absorbed = MathF.Min(absorbed, currentMoles);

        if (absorbed <= 0f)
            return;

        args.GasMixture.AdjustMoles(Gas, -absorbed);
        args.Supermatter.Comp.InternalEnergy += absorbed * EnergyPerMole;
    }
}
