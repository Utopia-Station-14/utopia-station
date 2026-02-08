using Content.Client._Utopia.Language;
using Content.Client._Utopia.Language.UI;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Shared._Utopia.Language;
using Content.Shared.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;

namespace Content.Client.UserInterface.Systems.Language;

public sealed class LanguageMenuUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    public LanguageMenuWindow? LangMenu;
    private MenuButton? LanguagesButton => UIManager.GetActiveUIWidgetOrNull<Content.Client.UserInterface.Systems.MenuBar.Widgets.GameTopMenuBar>()?.LanguageButton;

    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    public override void Initialize()
    {

        EntityManager.EventBus.SubscribeEvent<LanguageMenuStateMessage>(EventSource.All, this, OnStateUpdate);
    }

    private void OnStateUpdate(LanguageMenuStateMessage ev)
    {
        if (LangMenu == null)
            return;

        LangMenu.UpdateState(ev.CurrentLanguage, ev.Options, ev.TranslatorOptions);
    }


    public void OnStateEntered(GameplayState state)
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.OpenLanguageMenu,
                InputCmdHandler.FromDelegate(_ => ToggleLanguagesMenu()))
            .Register<LanguageMenuUIController>();
    }

    public void OnStateExited(GameplayState state)
    {
        CommandBinds.Unregister<LanguageMenuUIController>();
    }

    private void ToggleLanguagesMenu()
    {
        var player = _player.LocalEntity;
        if (!player.HasValue)
            return;

        if (LangMenu == null)
        {
            var lang = _entMan.System<LanguageSystem>();
            if (!lang.GetLanguages(player, out _, out var translator, out var current) || !lang.GetLanguagesKnowledged(player, LanguageKnowledge.Understand, out var langs, out _))
                return;

            // setup window
            LangMenu = UIManager.CreateWindow<LanguageMenuWindow>();
            LangMenu.Owner = player.Value;
            LangMenu.UpdateState(current, langs, translator);
            LangMenu.OnClose += OnWindowClosed;
            LangMenu.OnOpen += OnWindowOpen;
            LangMenu.OnLanguageSelected += OnLanguageSelected;

            if (LanguagesButton != null)
                LanguagesButton.SetClickPressed(true);

            LangMenu.OpenCentered();
        }
        else
        {
            LangMenu.OnClose -= OnWindowClosed;
            LangMenu.OnOpen -= OnWindowOpen;
            LangMenu.OnLanguageSelected -= OnLanguageSelected;

            if (LanguagesButton != null)
                LanguagesButton.SetClickPressed(false);

            CloseMenu();
        }
    }

    private void OnLanguageSelected(string lang)
    {
        var player = _player.LocalEntity;
        if (!player.HasValue)
            return;

        var ev = new LanguageChosenMessage(_entMan.GetNetEntity(player.Value), lang);
        _entMan.RaisePredictiveEvent(ev);
    }

    public void UnloadButton()
    {
        if (LanguagesButton == null)
            return;

        LanguagesButton.OnPressed -= ActionButtonPressed;
    }

    public void LoadButton()
    {
        if (LanguagesButton == null)
            return;

        LanguagesButton.OnPressed += ActionButtonPressed;
    }

    private void ActionButtonPressed(BaseButton.ButtonEventArgs args)
    {
        ToggleLanguagesMenu();
    }

    private void OnWindowClosed()
    {
        if (LanguagesButton != null)
            LanguagesButton.Pressed = false;

        CloseMenu();
    }

    private void OnWindowOpen()
    {
        if (LanguagesButton != null)
            LanguagesButton.Pressed = true;
    }

    private void CloseMenu()
    {
        if (LangMenu == null)
            return;

        LangMenu.Dispose();
        LangMenu = null;
    }
}
