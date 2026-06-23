using JetBrains.Annotations;

namespace Content.Client._Utopia.Economy.UI;

[UsedImplicitly]
public sealed class ATMBoundUserInterface : BoundUserInterface
{
    private AtmWindow _window;

    public ATMBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new AtmWindow();
    }

    protected override void Open()
    {
        base.Open();

        _window.OnWithdrawAttempt += SendMessage;

        if (State != null)
        {
            UpdateState(State);
        }

        _window.OnClose += Close;
        _window.OpenCentered();

    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window?.UpdateState(state);
    }

    protected override void Dispose(bool disposing)
    {
        _window?.Close();
        base.Dispose(disposing);
    }
}
