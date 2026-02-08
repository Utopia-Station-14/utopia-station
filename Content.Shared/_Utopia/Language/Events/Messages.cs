using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Language;

/// <summary>
///   Sent when a client wants to change its selected language.
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguageChosenMessage : EntityEventArgs
{
    public NetEntity Uid;
    public string SelectedLanguage;

    public LanguageChosenMessage(NetEntity uid, string selectedLanguage)
    {
        Uid = uid;
        SelectedLanguage = selectedLanguage;
    }
}

/// <summary>
///   Sent by the server when the client needs to update its language menu,
///   or directly after [RequestLanguageMenuStateMessage].
/// </summary>
[Serializable, NetSerializable]
public sealed class LanguageMenuStateMessage : EntityEventArgs
{
    public NetEntity ComponentOwner;
    public string CurrentLanguage;
    public Dictionary<string, LanguageKnowledge> Options;
    public Dictionary<string, LanguageKnowledge> TranslatorOptions;

    public LanguageMenuStateMessage(NetEntity componentOwner, string currentLanguage, Dictionary<string, LanguageKnowledge> options, Dictionary<string, LanguageKnowledge> translatorOptions)
    {
        ComponentOwner = componentOwner;
        CurrentLanguage = currentLanguage;
        Options = options;
        TranslatorOptions = translatorOptions;
    }
}
