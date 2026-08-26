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

        ProcessDamageAnnouncment(sm);
        sm.Comp.NextSpeechTime = _timing.CurTime + TimeSpan.FromSeconds(SpeechCooldown);
    }

    private void ProcessDamageAnnouncment(Entity<SupermatterComponent> sm)
    {
        var status = GetStatusType(sm);

        if (IsGlobal(sm, status))
            SendAnnouncement(sm, GetText(sm, status), GetColor(sm, status));
        else
            SendMessage(sm, GetText(sm, status));
    }

    public bool IsGlobal(Entity<SupermatterComponent> sm, SupermatterStatus status)
        => status is SupermatterStatus.Catastrophe or SupermatterStatus.Delamination;

    public Color GetColor(Entity<SupermatterComponent> sm, SupermatterStatus status)
        => status switch { SupermatterStatus.Delamination => DelaminationColor, _ => CatastropheColor };

    public string GetText(Entity<SupermatterComponent> sm, SupermatterStatus status)
    {
        var integrity = sm.Comp.Integrity.ToString();
        var text = status switch
        {
            _ when status is SupermatterStatus.Warning => Loc.GetString("supermatter-warning", ("integrity", integrity)),
            _ when status is SupermatterStatus.Catastrophe => Loc.GetString("supermatter-catastrophe", ("integrity", integrity)),
            _ when status is SupermatterStatus.Delamination => Loc.GetString("supermatter-delamination", ("integrity", integrity)),
            _ => Loc.GetString("supermatter-destabilization", ("integrity", integrity))
        };
        return text;
    }
}