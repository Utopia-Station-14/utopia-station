using Content.Shared._Utopia.Toxicology;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.Toxicology.UI;

[UsedImplicitly]
public sealed class ExplosionBeaconConsoleBoundUserInterface : BoundUserInterface
{
    private ExplosionBeaconConsoleWindow? _window;

    public ExplosionBeaconConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ExplosionBeaconConsoleWindow>();
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

        if (disposing)
            _window?.Dispose();
    }
}
