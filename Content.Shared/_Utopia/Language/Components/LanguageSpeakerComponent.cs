using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Language;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LanguageSpeakerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? CurrentLanguage = default!;

    /// <summary>
    /// Список языков, которые знает сущность. Писать в компонентах как:
    /// Прототип: Understand/BadSpeak/Speak
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, LanguageKnowledge> Languages = new();
}

[Serializable, NetSerializable]
public enum LanguageKnowledge : int
{
    Understand = 0,
    BadSpeak = 1,
    Speak = 2
}
