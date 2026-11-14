using Content.Shared.Atmos;

namespace Content.Shared._Utopia.Supermatter.Prototypes;

/// <summary>
///     Чинит кристалл за счёт газа. Газ тратится только на реально восстановленную целостность.
/// </summary>
public sealed partial class SupermatterHealingEffect : SupermatterGasReactionEffect
{
    [DataField(required: true)]
    public Gas Gas;

    [DataField]
    public float HealPerMole = 2f;

    [DataField]
    public float RatePerSecond = 0.1f;

    [DataField]
    public float MaxHealPerSecond = 1.5f;

    /// <summary>Верхняя граница целостности. Подставь реальную шкалу (100, 1 и т.п.).</summary>
    [DataField]
    public float MaxIntegrity = 100f;

    public override void Effect(SupermatterGasReactionEffectArgs args)
    {
        var comp = args.Supermatter.Comp;

        var missing = MaxIntegrity - comp.Integrity;
        if (missing <= 0f || HealPerMole <= 0f)
            return;

        var moles = args.GasMixture.GetMoles(Gas);
        if (moles <= 0f)
            return;

        var used = MathF.Min(moles, moles * RatePerSecond * args.FrameTime);

        var heal = MathF.Min(used * HealPerMole, MaxHealPerSecond * args.FrameTime);
        heal = MathF.Min(heal, missing);

        if (heal <= 0f)
            return;

        // Если упёрлись в лимит, тратим пропорционально меньше газа.
        used = heal / HealPerMole;

        args.GasMixture.AdjustMoles(Gas, -used);
        comp.Integrity += heal;
    }
}
