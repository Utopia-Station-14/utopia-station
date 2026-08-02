using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Access.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Systems;
using Content.Shared._Utopia.CCVar;
using Content.Shared._Utopia.Economy;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Economy;

public sealed class BankCardSystem : SharedEconomySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly BankCartridgeSystem _bankCartridge = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly JobSystem _job = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const string Salaries = "Salaries";
    private const int SalaryDelay = 2700;
    private const int FallbackBase = 100;

    private float _salaryTimer;

    public override void Initialize()
    {
        SubscribeLocalEvent<BankCardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_gameTicker.RunLevel != GameRunLevel.InRound)
        {
            _salaryTimer = 0f;
            return;
        }

        _salaryTimer += frameTime;

        if (_salaryTimer <= SalaryDelay)
            return;

        _salaryTimer = 0f;
        PayAutomaticSalary();
    }

    public void PayAutomaticSalary()
    {
        if (!_configManager.GetCVar(UCCVars.PaySalary))
            return;

        foreach (var account in Accounts.Where(account =>
            account.Mind != null
            && TryComp(GetEntity(account.Mind.Value), out MindComponent? mindComp)
            && mindComp.UserId != null
            && mindComp.CurrentEntity != null
            && _playerManager.ValidSessionId(mindComp.UserId.Value)
            && !_mobState.IsDead(mindComp.CurrentEntity.Value)))
        {
            if (account.Mind == null)
                continue;

            var mindUid = GetEntity(account.Mind.Value);
            var salaryProto = _protoMan.Index<SalaryPrototype>(Salaries);

            if (!TryGetSalaryEntry(mindUid, salaryProto, out var salary)
            || salary.Value.Salary == null)
                continue;

            if (account.IsBlocked)
                continue;

            if (salary.Value.Salary > 0)
            {
                TryChangeBalance(account.AccountId, salary.Value.Salary.Value);
            }
        }

        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("salary-pay-announcement"),
            colorOverride: Color.FromHex("#18abf5"));
    }

    public bool TryChangeBalance(int accountId, int amount)
    {
        if (!TryGetAccount(accountId, out var account))
            return false;

        if (account.CommandBudgetAccount && account.AccountPrototype != null)
        {
            var query = EntityQueryEnumerator<StationBankAccountComponent>();
            while (query.MoveNext(out var stationUid, out var stationBank))
            {
                if (stationBank.Accounts.TryGetValue(account.AccountPrototype.Value, out var currentBalance))
                {
                    if (currentBalance + amount < 0)
                        return false;

                    _cargo.UpdateBankAccount((stationUid, stationBank), amount, account.AccountPrototype.Value);
                    return true;
                }
            }
            return false;
        }

        if (account.Balance + amount < 0)
            return false;

        var operationType = amount > 0
            ? Loc.GetString("bank-deposit")
            : Loc.GetString("bank-withdrawal");

        account.Balance += amount;
        account.History ??= new List<TransactionsHistory>();
        account.History.Add(new TransactionsHistory(
            amount,
            _timing.CurTime,
            operationType,
            Loc.GetString("bank-system"),
            null
        ));

        if (account.CartridgeUid != null)
        {
            var args = new EconomyBalanceChangedEvent(operationType);
            RaiseLocalEvent(account.CartridgeUid.Value, ref args);

            _bankCartridge.UpdateUiState(account.CartridgeUid.Value);
        }

        return true;
    }

    public bool TryGetSalaryEntry(EntityUid? mind, ProtoId<SalaryPrototype> salaries, [NotNullWhen(true)] out SalaryEntry? salaryEntry)
    {
        salaryEntry = null;

        if (!_protoMan.TryIndex(salaries, out var salariesPrototype))
            return false;

        if (!_job.MindTryGetJob(mind, out var job))
            return false;

        if (!salariesPrototype.Salaries.TryGetValue(job.ID, out var entry))
            return false;

        salaryEntry = entry;
        return true;
    }

    private void OnMapInit(Entity<BankCardComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.CommandBudgetCard)
        {
            if (TryComp<StationBankAccountComponent>(_station.GetOwningStation(ent), out var stationBank)
                && ent.Comp.CommandBudgetType != null)
            {
                var existingAccount = Accounts.FirstOrDefault(acc =>
                    acc.CommandBudgetAccount &&
                    acc.AccountPrototype == ent.Comp.CommandBudgetType);

                if (existingAccount != null)
                {
                    ent.Comp.AccountId = existingAccount.AccountId;
                    return;
                }

                stationBank.BankAccounts.Add(
                    ent.Comp.CommandBudgetType.Value,
                    CreateBudgetAccount(ent.Comp.CommandBudgetType.Value)
                );

                stationBank.BankAccounts.TryGetValue(ent.Comp.CommandBudgetType.Value, out var account);

                if (account != null)
                {
                    ent.Comp.AccountId = account.AccountId;
                    return;
                }
            }
        }

        if (ent.Comp.AccountId.HasValue)
        {
            CreateAccount(ent.Comp.AccountId.Value, ent.Comp.StartingBalance);
            return;
        }

        var playerAccount = CreateAccount(default, ent.Comp.StartingBalance);
        ent.Comp.AccountId = playerAccount.AccountId;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        Accounts.Clear();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_idCardSystem.TryFindIdCard(ev.Mob, out var id) && TryComp<MindContainerComponent>(ev.Mob, out var mind))
        {
            var cardEntity = id.Owner;
            var bankCardComponent = EnsureComp<BankCardComponent>(cardEntity);
            var salaryProto = _protoMan.Index<SalaryPrototype>(Salaries);

            if (!bankCardComponent.AccountId.HasValue
            || !TryGetAccount(bankCardComponent.AccountId.Value, out var bankAccount))
                return;

            if (!TryComp(mind.Mind, out MindComponent? mindComponent))
                return;

            if (!TryGetSalaryEntry(mind.Mind, salaryProto, out var baseEntry))
                return;

            var roundtartBalance = baseEntry.Value.Roundstart ?? 0;

            bankAccount.Balance = roundtartBalance > 0
                ? roundtartBalance
                : FallbackBase;

            bankAccount.Mind = GetNetEntity(mind.Mind.Value);
            bankAccount.Name = Name(ev.Mob);

            mindComponent.AddMemory(new Memory("PIN", bankAccount.AccountPin.ToString()));
            mindComponent.AddMemory(new Memory(Loc.GetString("character-info-memories-account-number"),
                bankAccount.AccountId.ToString()));
        }
    }
}
