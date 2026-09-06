using Content.Shared._Utopia.Telescience.Components;
using Content.Shared._Utopia.Telescience.Messages;

namespace Content.Client._Utopia.Telescience.UI;

public sealed partial class TelescienceConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private IEntityManager _entMan = default!;

    private TelescienceWindow? _window;

    public TelescienceConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        if (!_entMan.TryGetComponent<TelescienceComputerComponent>(Owner, out var computer))
            return;

        base.Open();

        _window = new TelescienceWindow(Owner, computer);

        _window.OnClose += Close;
        _window.OpenCentered();

        _window.OnSendButtonPressed += location =>
            SendMessage(new TelescienceSendMessage(location));

        _window.OnRetrieveButtonPressed += location =>
            SendMessage(new TelescienceRetrieveMessage(location));

        _window.OnOpenPortalButtonPressed += location =>
            SendMessage(new TelescienceOpenPortalMessage(location));

        _window.OnClosePortalButtonPressed += () =>
            SendMessage(new TelescienceClosePortalMessage());

        _window.OnPositionChange += position =>
            SendMessage(new TelesciencePositionMessage(position));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Dispose();
    }
}
