using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Utopia.Economy;

public sealed class BankAccount
{
    public readonly int AccountId;
    public readonly int AccountPin;
    public int Balance;
    public bool CommandBudgetAccount;
    public NetEntity? Mind;
    public string Name = string.Empty;
    public ProtoId<CargoAccountPrototype>? AccountPrototype;
    public EntityUid? CartridgeUid;
    public List<TransactionsHistory> History;
    public bool IsBlocked;

    public BankAccount(int accountId, int balance, IRobustRandom random)
    {
        AccountId = accountId;
        Balance = balance;
        AccountPin = random.Next(1000, 10000);
        History = new List<TransactionsHistory>();
    }
}

