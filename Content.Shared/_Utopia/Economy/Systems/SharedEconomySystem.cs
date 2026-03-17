using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Utopia.Economy;

public abstract partial class SharedEconomySystem : EntitySystem
{
    [Dependency] protected readonly IRobustRandom Random = default!;

    protected readonly List<BankAccount> Accounts = new();

    protected BankAccount CreateAccount(int accountId = default, int startingBalance = 0)
    {
        if (TryGetAccount(accountId, out var acc))
            return acc;

        BankAccount account;
        if (accountId == default)
        {
            int accountNumber;

            do
            {
                accountNumber = Random.Next(100000, 999999);
            } while (AccountExist(accountNumber));

            account = new BankAccount(accountNumber, startingBalance, Random);
        }
        else
        {
            account = new BankAccount(accountId, startingBalance, Random);
        }

        Accounts.Add(account);
        return account;
    }

    protected BankAccount CreateBudgetAccount(ProtoId<CargoAccountPrototype> departmentType)
    {
        int accountNumber;
        do
        {
            accountNumber = Random.Next(100000, 999999);
        } while (AccountExist(accountNumber));

        var account = new BankAccount(accountNumber, 0, Random)
        {
            AccountPrototype = departmentType,
            CommandBudgetAccount = true,
            Name = Loc.GetString($"command-budget-{departmentType}")
        };

        Accounts.Add(account);
        return account;
    }

    public bool AccountExist(int accountId)
    {
        return Accounts.Any(x => x.AccountId == accountId);
    }

    public bool TryGetAccount(int accountId, [NotNullWhen(true)] out BankAccount? account)
    {
        account = Accounts.FirstOrDefault(x => x.AccountId == accountId);
        return account != null;
    }

    public int GetBalance(int accountId)
    {
        if (!TryGetAccount(accountId, out var account))
            return 0;

        if (account.CommandBudgetAccount && account.AccountPrototype != null)
        {
            var query = EntityQueryEnumerator<StationBankAccountComponent>();
            while (query.MoveNext(out var _, out var stationBank))
            {
                if (stationBank.Accounts.TryGetValue(account.AccountPrototype.Value, out var balance))
                    return balance;
            }
            return 0;
        }

        return account.Balance;
    }

    public bool ToggleAccount(BankAccount bankAccount)
    {
        return !bankAccount.IsBlocked;
    }
}

[ByRefEvent]
public record struct EconomyBalanceChangedEvent(string OperationType);
