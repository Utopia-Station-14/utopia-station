using JetBrains.Annotations;
using Content.Shared._Utopia.Economy;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.StationRecords;

namespace Content.Client._Utopia.Economy.UI;

[UsedImplicitly]
public sealed class SalaryConsoleBoundUserInterface : BoundUserInterface
{
    private SalaryConsoleMenu? _menu;

    public SalaryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _menu = new SalaryConsoleMenu();
        _menu.OnFiltersChanged += (type, filterValue) =>
            SendMessage(new SetStationRecordFilter(type, filterValue));

        _menu.OnKeySelected += key =>
            SendMessage(new SelectStationRecord(key));

        _menu.OnSendMoney += (recordKey, amount, pin) =>
            SendMessage(new SalaryConsoleSendMoneyMessage(recordKey, amount, pin));

        _menu.OnEjectCard += () =>
            SendMessage(new ItemSlotButtonPressedEvent(SalaryConsoleComponent.BudgetCardSlotId));

        _menu.OnClose += Close;
        _menu?.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _menu?.Dispose();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SalaryConsoleUserInterfaceState consoleState)
            return;

        _menu?.UpdateState(consoleState);
    }
}
