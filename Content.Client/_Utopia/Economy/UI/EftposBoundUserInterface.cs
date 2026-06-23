using JetBrains.Annotations;

namespace Content.Client._Utopia.Economy.UI;

[UsedImplicitly]
public sealed class EftposBoundUserInterface : BoundUserInterface
{
    private readonly EftposWindow _window;

    public EftposBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new EftposWindow();
    }

    protected override void Open()
    {
        base.Open();
        _window.OnCardButtonPressed += SendMessage;

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
        _window.UpdateState(state);
    }

    protected override void Dispose(bool disposing)
    {
        _window.Close();
        base.Dispose(disposing);
    }
}
