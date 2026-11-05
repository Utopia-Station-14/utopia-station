using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy.Messages;

[Serializable, NetSerializable]
public sealed class BankCartridgeUiState : BoundUserInterfaceState
{
    public int Balance;
    public int? AccountId = null;
    public string OwnerName = string.Empty;
    public string AccountLinkMessage = string.Empty;
    public string AccountLinkResult = string.Empty;
    public string TransferResult = string.Empty;
    public List<TransactionsHistory> History = [];
    public bool IsBlocked;
    public bool NotificationOn;
}

[Serializable, NetSerializable]
public sealed class BankAccountLinkMessage(int accountId, int pin) : CartridgeMessageEvent
{
    public int AccountId = accountId;
    public int Pin = pin;
}

[Serializable, NetSerializable]
public sealed class SetNotificationMessage : CartridgeMessageEvent { }

[Serializable, NetSerializable]
public sealed class BankTransferMessage(int accountTargetId, int pin, int amount) : CartridgeMessageEvent
{
    public int AccountTargetId = accountTargetId;
    public int Pin = pin;
    public int Amount = amount;
}
