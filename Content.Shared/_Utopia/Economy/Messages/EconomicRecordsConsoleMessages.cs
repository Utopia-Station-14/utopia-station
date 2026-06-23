using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy;

[Serializable, NetSerializable]
public enum EconomicRecordsConsoleKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class EconomicRecordsConsoleState : BoundUserInterfaceState
{
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly uint? SelectedKey;
    public readonly GeneralStationRecord? Record;
    public readonly StationRecordsFilter? Filter;
    public readonly int? AccountId;
    public readonly int Balance;
    public readonly List<TransactionsHistory> History;
    public readonly bool IsBlocked;

    public EconomicRecordsConsoleState(
        Dictionary<uint, string>? recordListing,
        uint? selectedKey,
        GeneralStationRecord? record,
        StationRecordsFilter? filter,
        int? accountId,
        int balance,
        List<TransactionsHistory> history,
        bool isBlocked)
    {
        RecordListing = recordListing;
        SelectedKey = selectedKey;
        Record = record;
        Filter = filter;
        AccountId = accountId;
        Balance = balance;
        History = history;
        IsBlocked = isBlocked;
    }
}

[Serializable, NetSerializable]
public sealed class EconomicRecordsBlockMessage() : BoundUserInterfaceMessage { }
