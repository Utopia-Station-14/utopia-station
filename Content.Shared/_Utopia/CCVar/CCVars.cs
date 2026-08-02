using Robust.Shared.Configuration;

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
}
