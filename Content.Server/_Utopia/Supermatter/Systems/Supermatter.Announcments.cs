using Content.Shared._Utopia.Supermatter.Components;
using Content.Server.AlertLevel;
using Content.Shared.Radio;
using Content.Shared.Chat;
using Robust.Shared.Prototypes;
using Content.Server.Radio.EntitySystems;
using Content.Server.Construction.Completions;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private RadioSystem _radio = default!;

    private const string AlertCodeCascade= "cascade";
    private const string AlertCodeDelta= "delta";
    private const string AlertCodeYellow = "yellow";

    private Color TeslaDelamination = Color.Turquoise;
    private Color SingularityDelamination = Color.Maroon;
    private Color ExplosionDelamination = Color.LightGoldenrodYellow;
    private Color CascadeDelamination = Color.DarkViolet;
    private Color DelaminationCanceled = Color.Orange;

    private static readonly ProtoId<RadioChannelPrototype> EngiChannel = "Engineering";
    private static readonly ProtoId<RadioChannelPrototype> CommonChannel = "Common";

    public void SendMessage(Entity<SupermatterComponent> sm, string text)
    {
        _chat.TrySendInGameICMessage(sm, text, InGameICChatType.Speak, hideChat: false, checkRadioPrefix: true);
        _radio.SendRadioMessage(sm, text, GetRadioChannel(sm), sm);
    }

    public void SendAnnouncement(Entity<SupermatterComponent> sm, string text, Color color)
    {
        var sender = Loc.GetString("supermatter-sender");
        _chat.DispatchStationAnnouncement(sm, text, sender, colorOverride: color);

    }

    private void ProcessSpeaking(Entity<SupermatterComponent> sm)
    {
        if (_timing.CurTime < sm.Comp.NextSpeechTime)
            return;

        if (sm.Comp.Integrity == MaxIntegrity)
            return;

        ProcessDamageAnnouncement(sm);
        sm.Comp.NextSpeechTime = _timing.CurTime + TimeSpan.FromSeconds(SpeechCooldown);
    }

    private void ProcessDamageAnnouncement(Entity<SupermatterComponent> sm)
    {
        var status = GetStatusType(sm);
        var damageType = sm.Comp.DamageType;

        string? text = (status, damageType) switch
        {
            (SupermatterStatus.Warning, SupermatterDamageType.Cascade) => Loc.GetString("supermatter-warning-cascade"),
            (SupermatterStatus.Warning, SupermatterDamageType.Heat) => Loc.GetString("supermatter-warning-hightemperature"),
            (SupermatterStatus.Warning, SupermatterDamageType.Energy) => Loc.GetString("supermatter-warning-energy"),
            (SupermatterStatus.Warning, SupermatterDamageType.Mole) => Loc.GetString("supermatter-warning-mole"),

            (SupermatterStatus.Destabilization, SupermatterDamageType.Cascade) => Loc.GetString("supermatter-destabilization-cascade"),
            (SupermatterStatus.Destabilization, SupermatterDamageType.Heat) => Loc.GetString("supermatter-destabilization-hightemperature"),
            (SupermatterStatus.Destabilization, SupermatterDamageType.Energy) => Loc.GetString("supermatter-destabilization-energy"),
            (SupermatterStatus.Destabilization, SupermatterDamageType.Mole) => Loc.GetString("supermatter-destabilization-mole"),
            _ => null
        };

        if (text != null)
            SendMessage(sm, text);
    }

    private void ProcessDelaminationAnnouncement(Entity<SupermatterComponent> sm)
    {
        if (!sm.Comp.Delamination)
        {
            SendAnnouncement(sm, Loc.GetString("supermatter-delamination-canceled"), DelaminationCanceled);
            return;
        }

        var delaminationType = GetDelaminationType(sm);
        var text = delaminationType switch
        {
            DelaminationType.Cascade => Loc.GetString("supermatter-delamination-cascade"),
            DelaminationType.Tesla => Loc.GetString("supermatter-delamination-tesla"),
            DelaminationType.Singularity => Loc.GetString("supermatter-delamination-singularity"),
            _ => Loc.GetString("supermatter-delamination-explosion"),
        };

        var remaining = sm.Comp.DelaminationEndTime - _timing.CurTime;
        var secondsLeft = Math.Max(0, remaining.TotalSeconds);
        var alertLevel = GetAlertLevel(sm, delaminationType);

        text += Loc.GetString("supermatter-seconds-before-delam", ("time", secondsLeft));
        _alert.SetLevel(sm, alertLevel, true, true, true, false);
        SendAnnouncement(sm, text, GetColor(sm, delaminationType));
    }


    private void HandleCountdown(Entity<SupermatterComponent> sm)
    {
        string text;
        SendMessage(sm, text);
        PlayAudio(sm, sound, false, false)
    }

    public ProtoId<RadioChannelPrototype> GetRadioChannel(Entity<SupermatterComponent> sm)
    {
        if (sm.Comp.Integrity > 50)
            return EngiChannel;

        return CommonChannel;
    }

    public Color GetColor(Entity<SupermatterComponent> sm, DelaminationType delaminationType)
    {
        var color = delaminationType switch
        {
            DelaminationType.Cascade => CascadeDelamination,
            DelaminationType.Tesla => TeslaDelamination,
            DelaminationType.Singularity => SingularityDelamination,
            _ => ExplosionDelamination
        };
        return color;
    }

    public string GetAlertLevel(Entity<SupermatterComponent> sm, DelaminationType delaminationType)
    {
        var level = delaminationType switch
        {
            DelaminationType.Cascade => AlertCodeCascade,
            DelaminationType.Explosion => AlertCodeYellow,
            _ => AlertCodeDelta
        };
        return level;
    }
}
