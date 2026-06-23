namespace Content.Server._Utopia.Chat;

[ByRefEvent]
public record struct CanHearVoiceEvent(EntityUid Source, bool Whisper, bool Cancelled = false);
