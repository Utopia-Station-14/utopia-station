using Content.Shared.Examine;
using Content.Shared.Radiation.Components;

namespace Content.Shared.Radiation.Systems;

public abstract class SharedGeigerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeigerComponent, ExaminedEvent>(OnExamine);
    }

    // Utopia-Tweak: RadiationUpdt
    private void OnExamine(EntityUid uid, GeigerComponent component, ExaminedEvent args)
    {
        if (!component.ShowExamine || !component.IsEnabled || !args.IsInDetailsRange)
            return;

        var betaStr = component.BetaRadiation.ToString("N1");
        var gammaStr = component.GammaRadiation.ToString("N1");

        var betaColor = LevelToColor(component.BetaDangerLevel);
        var gammaColor = LevelToColor(component.GammaDangerLevel);

        var betaMsg = Loc.GetString("geiger-component-examine-beta",
            ("rads", betaStr), ("color", betaColor));
        var gammaMsg = Loc.GetString("geiger-component-examine-gamma",
            ("rads", gammaStr), ("color", gammaColor));

        args.PushMarkup(betaMsg);
        args.PushMarkup(gammaMsg);
    }
    // Utopia-Tweak: RadiationUpdt

    public static Color LevelToColor(GeigerDangerLevel level)
    {
        switch (level)
        {
            case GeigerDangerLevel.None:
                return Color.Green;
            case GeigerDangerLevel.Low:
                return Color.Yellow;
            case GeigerDangerLevel.Med:
                return Color.DarkOrange;
            case GeigerDangerLevel.High:
            case GeigerDangerLevel.Extreme:
                return Color.Red;
            default:
                return Color.White;
        }
    }
}
