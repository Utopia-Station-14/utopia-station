using Content.Shared._Utopia.Language;

namespace Content.Shared.Speech;

public sealed class ListenEvent : EntityEventArgs
{
    public readonly LanguagePrototype? Language; // Utopia-Tweak : Language
    public readonly string Message;
    public readonly EntityUid Source;

    public ListenEvent(string message, EntityUid source, LanguagePrototype? language = null) // Utopia-Tweak : Language
    {
        Language = language; // Utopia-Tweak : Language
        Message = message;
        Source = source;
    }
}

public sealed class ListenAttemptEvent : CancellableEntityEventArgs
{
    public readonly EntityUid Source;

    public ListenAttemptEvent(EntityUid source)
    {
        Source = source;
    }
}
