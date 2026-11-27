using Content.Shared._Utopia.PDA.PdaPainter;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.PDA.PdaPainter;

[UsedImplicitly]
public sealed class PdaPainterBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private PdaPainterWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PdaPainterWindow>();

        _window.OnPaintPda += id => SendMessage(new PdaPainterPaintPdaMessage(id));
        _window.OnApplyCard += id => SendMessage(new PdaPainterApplyCardMessage(id));
        _window.OnEject += pda => SendMessage(new PdaPainterEjectMessage(pda));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PdaPainterBoundUserInterfaceState painterState)
        {
            _window?.UpdateState(painterState);
        }
    }
}
