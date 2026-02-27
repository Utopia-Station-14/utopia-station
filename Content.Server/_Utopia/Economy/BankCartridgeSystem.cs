using Content.Server.CartridgeLoader;
using Content.Shared._Utopia.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Economy;

public sealed class BankCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem? _cartridgeLoaderSystem = default!;
    [Dependency] private readonly BankCardSystem _bankCardSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BankCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeAddedEvent>(OnInstall);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeRemovedEvent>(OnRemove);
    }

    private void OnRemove(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeRemovedEvent args)
    {
        bankCartridge.Comp.Loader = null;
    }

    private void OnInstall(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeAddedEvent args)
    {
        bankCartridge.Comp.Loader = args.Loader;
    }

    private void OnAccountLink(Entity<BankCartridgeComponent> bankCartridge, BankAccountLinkMessage args)
    {
        if (!_bankCardSystem.TryGetAccount(args.AccountId, out var account)
        || args.Pin != account.AccountPin || account.CommandBudgetAccount)
        {
            bankCartridge.Comp.AccountLinkResult = Loc.GetString("bank-program-ui-link-error");
            return;
        }

        bankCartridge.Comp.AccountLinkResult = Loc.GetString("bank-program-ui-link-success");

        if (args.AccountId != bankCartridge.Comp.AccountId)
        {
            if (bankCartridge.Comp.AccountId != null
            && _bankCardSystem.TryGetAccount(bankCartridge.Comp.AccountId.Value, out var oldAccount)
            && oldAccount.CartridgeUid == bankCartridge)
                oldAccount.CartridgeUid = null;

            if (account.CartridgeUid != null)
                Comp<BankCartridgeComponent>(account.CartridgeUid.Value).AccountId = null;

            account.CartridgeUid = bankCartridge;
            bankCartridge.Comp.AccountId = args.AccountId;
        }

        if (!TryComp(GetEntity(args.LoaderUid), out PdaComponent? pda) || !pda.ContainedId.HasValue
        || HasComp<BankCardComponent>(pda.ContainedId.Value))
            return;

        var bankCard = AddComp<BankCardComponent>(pda.ContainedId.Value);
        bankCard.AccountId = account.AccountId;
    }

    private void OnTransfer(Entity<BankCartridgeComponent> bankCartridge, BankTransferMessage args)
    {
        if (bankCartridge.Comp.AccountId == null
        || !_bankCardSystem.TryGetAccount(bankCartridge.Comp.AccountId.Value, out var senderAccount))
        {
            bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-error-no-account");
            return;
        }

        if (senderAccount.AccountPin != args.Pin)
        {
            bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-error-pin");
            return;
        }

        if (!_bankCardSystem.TryGetAccount(args.AccountTargetId, out var targetAccount))
        {
            bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-error-target");
            return;
        }

        if (args.Amount <= 0)
        {
            bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-error-funds");
            return;
        }

        if (senderAccount.Balance < args.Amount)
        {
            bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-error-funds");
            return;
        }

        senderAccount.Balance -= args.Amount;
        targetAccount.Balance += args.Amount;

        senderAccount.History ??= new List<TransactionsHistory>();
        targetAccount.History ??= new List<TransactionsHistory>();

        senderAccount.History.Add(new TransactionsHistory(
            -args.Amount,
            _timing.CurTime,
            Loc.GetString("bank-transfer"),
            targetAccount.Name,
            targetAccount.AccountId.ToString()
        ));

        targetAccount.History.Add(new TransactionsHistory(
            args.Amount,
            _timing.CurTime,
            Loc.GetString("bank-transfer"),
            senderAccount.Name,
            senderAccount.AccountId.ToString()
        ));

        if (senderAccount.CartridgeUid != null)
            UpdateUiState(senderAccount.CartridgeUid.Value, bankCartridge.Comp.Loader!.Value);

        if (targetAccount.CartridgeUid != null && targetAccount.CartridgeUid != senderAccount.CartridgeUid)
            UpdateUiState(targetAccount.CartridgeUid.Value, bankCartridge.Comp.Loader!.Value);

        bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-success",
            ("amount", args.Amount), ("target", targetAccount.Name));
    }

    private void OnUiReady(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(bankCartridge, args.Loader, bankCartridge.Comp);
    }

    private void OnUiMessage(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeMessageEvent args)
    {
        if (args is BankAccountLinkMessage message)
            OnAccountLink(bankCartridge, message);

        if (args is BankTransferMessage transferMessage)
            OnTransfer(bankCartridge, transferMessage);

        UpdateUiState(bankCartridge, GetEntity(args.LoaderUid), bankCartridge.Comp);
    }

    private void UpdateUiState(EntityUid cartridgeUid, EntityUid loaderUid, BankCartridgeComponent? component = null)
    {
        if (!Resolve(cartridgeUid, ref component))
            return;

        var accountLinkMessage = Loc.GetString("bank-program-ui-link-program") + '\n';
        if (TryComp(loaderUid, out PdaComponent? pda) && pda.ContainedId.HasValue)
        {
            accountLinkMessage += TryComp(pda.ContainedId.Value, out BankCardComponent? bankCard)
                ? Loc.GetString("bank-program-ui-link-id-card-linked", ("account", bankCard.AccountId!.Value))
                : Loc.GetString("bank-program-ui-link-id-card");
        }
        else
        {
            accountLinkMessage += Loc.GetString("bank-program-ui-link-no-id-card");
        }

        var state = new BankCartridgeUiState
        {
            AccountLinkResult = component.AccountLinkResult,
            AccountLinkMessage = accountLinkMessage,
            TransferResult = component.TransferResult
        };

        if (component.AccountId != null && _bankCardSystem.TryGetAccount(component.AccountId.Value, out var account))
        {
            state.Balance = account.Balance;
            state.AccountId = account.AccountId;
            state.OwnerName = account.Name;
            state.History = account.History ?? new List<TransactionsHistory>();
        }

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }

    public void UpdateUiState(EntityUid cartridgeUid)
    {
        if (!TryComp(cartridgeUid, out BankCartridgeComponent? component) || component.Loader == null)
            return;

        UpdateUiState(cartridgeUid, component.Loader.Value, component);
    }
}
