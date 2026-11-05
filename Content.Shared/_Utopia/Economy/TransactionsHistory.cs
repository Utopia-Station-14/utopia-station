using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy;

[Serializable, NetSerializable]
public sealed class TransactionsHistory(int amount, TimeSpan timestamp, string type, string initiatorName, string? targetId)
{
    public int Amount = amount;
    public TimeSpan Timestamp = timestamp;
    public string Type = type;
    public string InitiatorName = initiatorName;
    public string? TargetId = targetId;
}
