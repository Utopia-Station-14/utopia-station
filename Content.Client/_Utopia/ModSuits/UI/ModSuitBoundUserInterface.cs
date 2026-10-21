using Content.Shared._Utopia.ModSuits;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.ModSuits.UI;

[UsedImplicitly]
public sealed class ModSuitBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ModSuitMenu? _menu;

    public ModSuitBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindowCenteredLeft<ModSuitMenu>();
        _menu.SetOwner(Owner);

        _menu.OnPartPressed += part => SendMessage(new ModSuitTogglePartMessage(part));
        _menu.OnToggleAllPressed += () => SendMessage(new ModSuitToggleAllMessage());
        _menu.OnToggleSpeedPressed += () => SendMessage(new ModSuitToggleSpeedMessage());
        _menu.OnModulePressed += module => SendMessage(new ModSuitToggleModuleMessage(module));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not ModSuitBoundUiState suitState)
            return;

        _menu?.UpdateState(suitState);
    }
}
