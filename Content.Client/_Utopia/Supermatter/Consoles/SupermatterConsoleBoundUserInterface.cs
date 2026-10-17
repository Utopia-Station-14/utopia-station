using Content.Shared._Utopia.Supermatter.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.Supermatter.Consoles;

[UsedImplicitly]
public sealed class SupermatterConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private SupermatterConsoleMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SupermatterConsoleMenu>();
        _menu.OnClose += Close;

        _menu.OnEntrySelected += netEntity =>
        {
            SendMessage(new SupermatterConsoleFocusChangeMessage(netEntity));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SupermatterConsoleBoundInterfaceState smState)
            return;

        if (_menu is not { Disposed: false })
            return;

        _menu.UpdateState(smState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Close();
        _menu = null;
    }
}
