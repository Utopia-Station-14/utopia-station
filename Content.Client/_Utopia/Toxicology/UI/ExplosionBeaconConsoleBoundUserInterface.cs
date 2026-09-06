using Content.Shared._Utopia.Toxicology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.Toxicology.UI;

[UsedImplicitly]
public sealed class ExplosionBeaconConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ExplosionBeaconConsoleWindow? _window;
    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ExplosionBeaconConsoleWindow>();

        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ExplosionBeaconConsoleState cast)
            _window?.UpdateState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _window?.Dispose();
    }
}
