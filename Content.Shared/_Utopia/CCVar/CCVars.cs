using Robust.Shared.Configuration;
using Content.Shared.Atmos;

namespace Content.Shared._Utopia.CCVar;

[CVarDefs]
public sealed class UCCVars
{
    #region Barks
    public static readonly CVarDef<bool> BarksEnabled =
        CVarDef.Create("barks.enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMaxPitch =
        CVarDef.Create("barks.max_pitch", 1.5f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMinPitch =
        CVarDef.Create("barks.min_pitch", 0.6f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMinDelay =
        CVarDef.Create("barks.min_delay", 0.1f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksMaxDelay =
        CVarDef.Create("barks.max_delay", 0.6f, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<float> BarksVolume =
        CVarDef.Create("barks.volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
    #endregion

    #region ZLevels
    public static readonly CVarDef<bool> FallToBackroomsEnabled =
        CVarDef.Create("fall_to_backrooms.enabled", false, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool>
        CEZProjectedLightingEnabled = CVarDef.Create("zlevels.ce_projected_lighting_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Maximum number of projected lights per adjacent Z layer. Caps render cost. Default 16.</summary>
    public static readonly CVarDef<int>
        CEZMaxProjectedLightsPerLevel = CVarDef.Create("zlevels.ce_max_projected_lights_per_level", 16, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How much each depth step attenuates projected light energy. Higher = darker further away.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightAttenuationPerDepth = CVarDef.Create("zlevels.ce_projected_light_attenuation_per_depth", 0.75f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>How much each tile of distance from source to opening attenuates projected light energy.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightAttenuationPerTile = CVarDef.Create("zlevels.ce_projected_light_attenuation_per_tile", 0.25f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Maximum radius for any single projected light. Caps individual brightness footprint.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightMaxRadius = CVarDef.Create("zlevels.ce_projected_light_max_radius", 4f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Multiplier on the source-light remaining-radius to compute projected radius.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightRadiusScale = CVarDef.Create("zlevels.ce_projected_light_radius_scale", 0.6f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Energy floor below which a projected light is discarded as imperceptible.</summary>
    public static readonly CVarDef<float>
        CEZProjectedLightMinEnergy = CVarDef.Create("zlevels.ce_projected_light_min_energy", 0.1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// If true, audio playing on a Z-network map is also projected to adjacent Z layers through
    /// floor/ceiling openings. Adds ~one PlayStatic call per audible adjacent layer per
    /// audio entity. Toggle off to disable cross-Z hearing.
    /// </summary>
    public static readonly CVarDef<bool>
        CEZLevelsCrossZAudio = CVarDef.Create("zlevels.ce_cross_z_audio", true, CVar.SERVERONLY);

    /// <summary>
    /// Debug-only: log every decision gate in the cross-Z audio projection pipeline. Use to
    /// figure out why a given sound isn't reaching a listener on an adjacent level.
    /// </summary>
    public static readonly CVarDef<bool>
        CEZLevelsCrossZAudioDebug = CVarDef.Create("zlevels.ce_cross_z_audio_debug", false, CVar.SERVERONLY);

    /// <summary>Max world distance a cross-Z shot may travel. Cross-Z is intentionally close-quarters. Default 4.</summary>
    public static readonly CVarDef<float>
        CEZShootingRange = CVarDef.Create("zlevels.ce_shooting_range", 4f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>Max tile distance from the shooter to an eligible floor-opening center. Default 2.</summary>
    public static readonly CVarDef<float>
        CEZShootingOpeningTileRange = CVarDef.Create("zlevels.ce_shooting_opening_tile_range", 2f, CVar.SERVER | CVar.REPLICATED);
    #endregion

    // Economy
    public static readonly CVarDef<bool> PaySalary =
        CVarDef.Create("economy.pay_salary", true, CVar.SERVERONLY);

    // Combat
    public static readonly CVarDef<bool> CombatShowIcons =
        CVarDef.Create("combat.show_icons", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    #region Supermatter
    public static readonly CVarDef<bool> SupermatterDoCascadeDelam =
        CVarDef.Create("supermatter.do_cascade", true, CVar.SERVER);

    /// <summary>
    ///     The supermatter gains +1 bolts of electricity.
    /// </summary>
    public static readonly CVarDef<float> SupermatterPowerMinPenaltyThreshold =
        CVarDef.Create("supermatter.power_min_penalty_threshold", 3000f, CVar.SERVER);

    /// <summary>
    ///     The cutoff on power properly doing damage, pulling shit around.
    ///     The supermatter will also spawn anomalies, and gains +2 bolts of electricity.
    /// </summary>
    public static readonly CVarDef<float> SupermatterPowerPenaltyThreshold =
        CVarDef.Create("supermatter.power_penalty_threshold", 5000f, CVar.SERVER);

    /// <summary>
    ///     Above this, the supermatter spawns anomalies at an increased rate, and gains +1 bolt of electricity.
    /// </summary>
    public static readonly CVarDef<float> SupermatterSeverePowerPenaltyThreshold =
        CVarDef.Create("supermatter.power_penalty_threshold_severe", 7000f, CVar.SERVER);

    /// <summary>
    ///     Above this, the supermatter spawns pyro anomalies at an increased rate, and gains +1 bolt of electricity.
    /// </summary>
    public static readonly CVarDef<float> SupermatterCriticalPowerPenaltyThreshold =
        CVarDef.Create("supermatter.power_penalty_threshold_critical", 9000f, CVar.SERVER);

    /// <summary>
    ///     The minimum pressure for a pure ammonia atmosphere to begin being consumed.
    /// </summary>
    public static readonly CVarDef<float> SupermatterAmmoniaConsumptionPressure =
        CVarDef.Create("supermatter.ammonia_consumption_pressure", Atmospherics.OneAtmosphere * 0.01f, CVar.SERVER);

    /// <summary>
    ///     How the amount of ammonia consumed per tick scales with partial pressure.
    /// </summary>
    public static readonly CVarDef<float> SupermatterAmmoniaPressureScaling =
        CVarDef.Create("supermatter.ammonia_pressure_scaling", Atmospherics.OneAtmosphere * 0.05f, CVar.SERVER);

    /// <summary>
    ///     How much the amount of ammonia consumed per tick scales with the gas mix power ratio.
    /// </summary>
    public static readonly CVarDef<float> SupermatterAmmoniaGasMixScaling =
        CVarDef.Create("supermatter.ammonia_gas_mix_scaling", 0.3f, CVar.SERVER);

    /// <summary>
    ///     The amount of matter power generated for every mole of ammonia consumed.
    /// </summary>
    public static readonly CVarDef<float> SupermatterAmmoniaPowerGain =
        CVarDef.Create("supermatter.ammonia_power_gain", 10f, CVar.SERVER);

    /// <summary>
    ///     When true, bypass the normal checks to determine delam type, and instead use the type chosen by supermatter.forced_delam_type
    /// </summary>
    public static readonly CVarDef<bool> SupermatterDoForceDelam =
        CVarDef.Create("supermatter.do_force_delam", false, CVar.SERVER);

    /// <summary>
    ///     Maximum safe operational temperature in degrees Celsius.
    ///     Supermatter begins taking damage above this temperature.
    /// </summary>
    public static readonly CVarDef<float> SupermatterHeatPenaltyThreshold =
        CVarDef.Create("supermatter.heat_penalty_threshold", 40f, CVar.SERVER);

    /// <summary>
    ///     The percentage of the supermatter's matter power that is converted into power each atmos tick.
    /// </summary>
    public static readonly CVarDef<float> SupermatterMatterPowerConversion =
        CVarDef.Create("supermatter.matter_power_conversion", 10f, CVar.SERVER);

    /// <summary>
    ///     Divisor on the amount of gas absorbed by the supermatter during the roundstart grace period.
    /// </summary>
    public static readonly CVarDef<float> SupermatterGasEfficiencyGraceModifier =
        CVarDef.Create("supermatter.gas_efficiency_grace_modifier", 2.5f, CVar.SERVER);

    /// <summary>
    ///     Divisor on the amount of damage that the supermatter takes from absorbing hot gas.
    /// </summary>
    public static readonly CVarDef<float> SupermatterMoleHeatPenalty =
        CVarDef.Create("supermatter.mole_heat_penalty", 350f, CVar.SERVER);

    /// <summary>
    ///     Above this threshold the supermatter will delaminate into a singulo and take damage from gas moles.
    ///     Below this threshold, the supermatter can heal damage.
    /// </summary>
    public static readonly CVarDef<float> SupermatterMolePenaltyThreshold =
        CVarDef.Create("supermatter.mole_penalty_threshold", 100f, CVar.SERVER);

    /// <summary>
    ///     Divisor on the amount of oxygen released during atmospheric reactions.
    /// </summary>
    public static readonly CVarDef<float> SupermatterOxygenReleaseModifier =
        CVarDef.Create("supermatter.oxygen_release_modifier", 325f, CVar.SERVER);

    /// <summary>
    ///     Divisor on the amount of plasma released during atmospheric reactions.
    /// </summary>
    public static readonly CVarDef<float> SupermatterPlasmaReleaseModifier =
        CVarDef.Create("supermatter.plasma_release_modifier", 750f, CVar.SERVER);

    /// <summary>
    ///     Percentage of inhibitor gas needed before the charge inertia chain reaction effect starts.
    /// </summary>
    public static readonly CVarDef<float> SupermatterPowerlossInhibitionGasThreshold =
        CVarDef.Create("supermatter.powerloss_inhibition_gas_threshold", 0.2f, CVar.SERVER);

    /// <summary>
    ///     Moles of the gas needed before the charge inertia chain reaction effect starts.
    ///     Scales powerloss inhibition down until this amount of moles is reached.
    /// </summary>
    public static readonly CVarDef<float> SupermatterPowerlossInhibitionMoleThreshold =
        CVarDef.Create("supermatter.powerloss_inhibition_mole_threshold", 12f, CVar.SERVER);

    /// <summary>
    ///     Bonus powerloss inhibition boost if this amount of moles is reached.
    /// </summary>
    public static readonly CVarDef<float> SupermatterPowerlossInhibitionMoleBoostThreshold =
        CVarDef.Create("supermatter.powerloss_inhibition_mole_boost_threshold", 500f, CVar.SERVER);

    /// <summary>
    ///     Base amount of radiation that the supermatter emits.
    /// </summary>
    public static readonly CVarDef<float> SupermatterRadsBase =
        CVarDef.Create("supermatter.rads_base", 4f, CVar.SERVER);

    /// <summary>
    ///     Directly multiplies the amount of rads put out by the supermatter. Be VERY conservative with this.
    /// </summary>
    public static readonly CVarDef<float> SupermatterRadsModifier =
        CVarDef.Create("supermatter.rads_modifier", 1f, CVar.SERVER);

    /// <summary>
    ///     Multiplier on the overall power produced during supermatter atmospheric reactions.
    /// </summary>
    public static readonly CVarDef<float> SupermatterReactionPowerModifier =
        CVarDef.Create("supermatter.reaction_power_modifier", 0.55f, CVar.SERVER);

    /// <summary>
    ///     Divisor on the amount that atmospheric reactions increase the supermatter's temperature.
    /// </summary>
    public static readonly CVarDef<float> SupermatterThermalReleaseModifier =
        CVarDef.Create("supermatter.thermal_release_modifier", 5f, CVar.SERVER);

    /// <summary>
    ///     How often the supermatter should announce its status.
    /// </summary>
    public static readonly CVarDef<float> SupermatterYellTimer =
        CVarDef.Create("supermatter.yell_timer", 60f, CVar.SERVER);
    #endregion
}