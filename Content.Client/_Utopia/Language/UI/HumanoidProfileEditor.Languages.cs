using System.Linq;
using Content.Client._Utopia.Language.UI;
using Content.Shared._Utopia.Language;
using Content.Shared.Humanoid.Prototypes;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public void RefreshLanguages()
    {
        LanguagesList.DisposeAllChildren();
        TabContainer.SetTabTitle(1, Loc.GetString("humanoid-profile-editor-languages-tab"));
        SetDefaultLanguagesButton.OnPressed += _ => SetDefaultLanguages();

        if (Profile == null)
            return;

        var species = _prototypeManager.Index(Profile.Species);

        LanguagesCountLabel.Text = Loc.GetString(
            "humanoid-profile-editor-languages-count",
            ("current", Profile.Languages.Count),
            ("max", species.MaxLanguages)
        );

        var availableLanguages = GetAvailableLanguages(species);
        var defaultLanguages = GetDefaultLanguages(availableLanguages, species);

        AddLanguages(defaultLanguages, species);
        AddLanguages(availableLanguages.Except(defaultLanguages), species);
    }

    private List<LanguagePrototype> GetAvailableLanguages(SpeciesPrototype species)
    {
        var languages = _prototypeManager
            .EnumeratePrototypes<LanguagePrototype>()
            .Where(x => x.Roundstart)
            .ToList();

        foreach (var uniqueId in species.UniqueLanguages)
        {
            languages.Add(_prototypeManager.Index(uniqueId));
        }

        return SortLanguages(languages);
    }

    private List<LanguagePrototype> GetDefaultLanguages(List<LanguagePrototype> available, SpeciesPrototype species)
    {
        var defaults = new List<LanguagePrototype>();

        var standardDefaults = available.Where(x =>
            species.DefaultLanguages.Contains(x) &&
            !species.UniqueLanguages.Contains(x)
        );

        defaults.AddRange(standardDefaults);

        var uniqueDefaults = available.Where(x => species.UniqueLanguages.Contains(x));
        defaults.AddRange(uniqueDefaults);

        return SortLanguages(defaults);
    }

    private List<LanguagePrototype> SortLanguages(List<LanguagePrototype> languages)
    {
        var sorted = new List<LanguagePrototype>(languages);
        sorted.Sort((x, y) => x.LocalizedName[0].CompareTo(y.LocalizedName[0]));
        sorted.Sort((x, y) => y.Priority.CompareTo(x.Priority));
        return sorted;
    }

    private void AddLanguages(IEnumerable<LanguagePrototype> languages, SpeciesPrototype species)
    {
        foreach (var language in languages)
        {
            AddLanguageEntry(language, species);
        }
    }

    private void AddLanguageEntry(LanguagePrototype proto, SpeciesPrototype species)
    {
        if (Profile == null)
            return;

        var entry = new LanguageEntry(proto, false)
        {
            Margin = new(7),
            HorizontalExpand = true
        };

        var isAlreadySelected = Profile.Languages.Contains(proto);
        var maxReached = Profile.Languages.Count >= species.MaxLanguages;

        entry.SelectButton.Text = Loc.GetString(isAlreadySelected
            ? "language-lobby-remove-button"
            : "language-lobby-add-button"
        );

        entry.SelectButton.ToolTip = null;
        entry.SelectButton.Disabled = maxReached && !isAlreadySelected;

        entry.OnLanguageSelected += SelectLanguage;
        LanguagesList.AddChild(entry);
    }

    public void SelectLanguage(string protoId)
    {
        var isAlreadySelected = Profile?.Languages.Contains(protoId) ?? false;

        Profile = isAlreadySelected
            ? Profile?.WithoutLanguage(protoId)
            : Profile?.WithLanguage(protoId);

        SetDirty();
        RefreshLanguages();
    }

    public void SetDefaultLanguages()
    {
        if (Profile == null)
            return;

        var species = _prototypeManager.Index(Profile.Species);

        foreach (var language in Profile.Languages)
        {
            Profile = Profile?.WithoutLanguage(language);
        }

        foreach (var defaultLanguage in species.DefaultLanguages)
        {
            Profile = Profile?.WithLanguage(defaultLanguage);
        }

        SetDirty();
        RefreshLanguages();
    }
}
