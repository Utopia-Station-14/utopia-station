using Content.Client._Utopia.ZLevels.Ladders;
using Content.Shared._Utopia.ZLevels.Components;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.ZLevels.Ladders;

public sealed class ZLevelLadderBoundUserInterface : BoundUserInterface
{
    private ZLevelLadderMenu? _menu;

    public ZLevelLadderBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ZLevelLadderMenu>();
        _menu.SetEntity(Owner);

        _menu.OnSelected += dir =>
        {
            SendMessage(new ZLevelLadderMessage(dir));
            Close();
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ZLevelLadderBuiState castState)
        {
            _menu?.UpdateMenu(castState.Directions);
        }
    }
}
