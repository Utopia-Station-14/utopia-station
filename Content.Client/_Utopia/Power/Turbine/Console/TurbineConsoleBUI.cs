using Content.Shared.Power.Turbines.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Power.Turbines;

public sealed class TurbineConsoleBoundUserInterface : BoundUserInterface
{
    private TurbineConsoleWindow? _window;

    public TurbineConsoleBoundUserInterface(EntityUid owner, Enum uiKey)
        : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new TurbineConsoleWindow();
        _window.OnClose += Close;
        _window.OnTurbineSelected += OnTurbineSelected;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not TurbineConsoleBoundInterfaceState turbineState)
            return;

        _window?.UpdateState(turbineState);
    }

    private void OnTurbineSelected(NetEntity? turbine)
    {
        SendMessage(new TurbineConsoleFocusChangeMessage(turbine));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
    }
}