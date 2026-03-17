using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy;

[Serializable, NetSerializable]
public sealed class SalaryConsoleUserInterfaceState : BoundUserInterfaceState
{
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly uint? SelectedKey;
    public readonly GeneralStationRecord? Record;
    public readonly StationRecordsFilter? Filter;
    public readonly bool BudgetCardInserted;
    public readonly int BudgetCardBalance;
    public readonly string? BudgetCardLabel;

    public SalaryConsoleUserInterfaceState(
        Dictionary<uint, string>? recordListing,
        uint? selectedKey,
        GeneralStationRecord? record,
        StationRecordsFilter? filter,
        bool budgetCardInserted,
        int budgetCardBalance,
        string? budgetCardLabel)
    {
        RecordListing = recordListing;
        SelectedKey = selectedKey;
        Record = record;
        Filter = filter;
        BudgetCardInserted = budgetCardInserted;
        BudgetCardBalance = budgetCardBalance;
        BudgetCardLabel = budgetCardLabel;
    }
}

[Serializable, NetSerializable]
public sealed class SalaryConsoleSendMoneyMessage(uint recordKey, int amount, int pin) : BoundUserInterfaceMessage
{
    public readonly uint RecordKey = recordKey;
    public readonly int Amount = amount;
    public readonly int Pin = pin;
}
