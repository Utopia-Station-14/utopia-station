using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Language;

/// <summary>
/// This component allows holding entity to understand given languages if it can understand any of "toSpeak" languages
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class HandheldTranslatorComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<string, LanguageKnowledge> Languages;

    [DataField("toggle")]
    public bool ToggleOnInteract = true;

    [DataField, AutoNetworkedField]
    public bool Enabled = false;

    public EntityUid? User;
}
