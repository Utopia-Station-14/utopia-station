using Content.Shared.StationRecords;
using Content.Shared._Utopia.Economy;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.Economy.UI;

[UsedImplicitly]
public sealed class EconomicRecordsConsoleBoundUserInterface : BoundUserInterface
{
    private EconomicRecordsConsoleWindow? _window;

    public EconomicRecordsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<EconomicRecordsConsoleWindow>();
        _window.OnKeySelected += key =>
            SendMessage(new SelectStationRecord(key));

        _window.OnFiltersChanged += (type, filterValue) =>
            SendMessage(new SetStationRecordFilter(type, filterValue));

        _window.OnBlockToggle += () =>
            SendMessage(new EconomicRecordsBlockMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not EconomicRecordsConsoleState cast)
            return;

        _window?.UpdateState(cast);
        _window?.UpdateCheckBox(cast.IsBlocked);
    }
}
