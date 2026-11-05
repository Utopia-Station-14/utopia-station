using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy.Messages;

[Serializable, NetSerializable]
public enum EconomicRecordsConsoleKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class EconomicRecordsConsoleState(
    Dictionary<uint, string>? recordListing,
    uint? selectedKey,
    GeneralStationRecord? record,
    StationRecordsFilter? filter,
    int? accountId,
    int balance,
    List<TransactionsHistory> history,
    bool isBlocked) : BoundUserInterfaceState
{
    public readonly Dictionary<uint, string>? RecordListing = recordListing;
    public readonly uint? SelectedKey = selectedKey;
    public readonly GeneralStationRecord? Record = record;
    public readonly StationRecordsFilter? Filter = filter;
    public readonly int? AccountId = accountId;
    public readonly int Balance = balance;
    public readonly List<TransactionsHistory> History = history;
    public readonly bool IsBlocked = isBlocked;
}

[Serializable, NetSerializable]
public sealed class EconomicRecordsBlockMessage() : BoundUserInterfaceMessage { }
