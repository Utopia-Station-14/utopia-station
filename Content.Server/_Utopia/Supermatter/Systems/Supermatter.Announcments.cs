using Content.Shared._Utopia.Supermatter.Components;
using Content.Server.AlertLevel;
using Content.Shared.Chat;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private AlertLevelSystem _alert = default!;
    [Dependency] private SharedChatSystem _chat = default!;

    private Color CatastropheColor = Color.Yellow;
    private Color DelaminationColor = Color.Orange;

    public void SendMessage(Entity<SupermatterComponent> sm, string text)
    {
        _chat.TrySendInGameICMessage(sm, text, InGameICChatType.Speak, hideChat: false, checkRadioPrefix: true);
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
            (SupermatterStatus.Warning, SupermatterDamageType.Heat) => Loc.GetString("supermatter-warning-hightemperature"),
            (SupermatterStatus.Warning, SupermatterDamageType.Energy) => Loc.GetString("supermatter-warning-energy"),
            (SupermatterStatus.Warning, SupermatterDamageType.Mole) => Loc.GetString("supermatter-warning-mole"),

            (SupermatterStatus.Destabilization, SupermatterDamageType.Heat) => Loc.GetString("supermatter-destabilization-hightemperature"),
            (SupermatterStatus.Destabilization, SupermatterDamageType.Energy) => Loc.GetString("supermatter-destabilization-energy"),
            (SupermatterStatus.Destabilization, SupermatterDamageType.Mole) => Loc.GetString("supermatter-destabilization-mole"),
            _ => null
        };

        if (text != null)
            SendMessage(sm, text);
    }

    public bool IsGlobal(Entity<SupermatterComponent> sm, SupermatterStatus status)
        => status is SupermatterStatus.Catastrophe or SupermatterStatus.Delamination;

    public Color GetColor(Entity<SupermatterComponent> sm, SupermatterStatus status)
        => status switch { SupermatterStatus.Delamination => DelaminationColor, _ => CatastropheColor };

}
