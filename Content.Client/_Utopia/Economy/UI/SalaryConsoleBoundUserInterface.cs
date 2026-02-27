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

        _menu = new SalaryConsoleMenu(this);
        _menu.OnFiltersChanged += (type, filterValue) =>
            SendMessage(new SetStationRecordFilter(type, filterValue));

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

    public void EjectCard()
    {
        SendMessage(new ItemSlotButtonPressedEvent(SalaryConsoleComponent.BudgetCardSlotId));
    }

    public void SelectRecord(uint? key)
    {
        SendMessage(new SalaryConsoleSelectRecordMessage(key));
    }

    public void SendMoney(uint recordKey, int amount)
    {
        SendMessage(new SalaryConsoleSendMoneyMessage(recordKey, amount));
    }
}
