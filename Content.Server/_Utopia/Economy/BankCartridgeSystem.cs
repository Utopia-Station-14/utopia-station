using Content.Server.CartridgeLoader;
using Content.Shared._Utopia.Economy;
using Content.Shared.CartridgeLoader;
using Content.Shared.PDA;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Economy;

public sealed class BankCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private readonly BankCardSystem _bankCardSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BankCartridgeComponent, EconomyBalanceChangedEvent>(OnBalanceChanged);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeAddedEvent>(OnInstall);
        SubscribeLocalEvent<BankCartridgeComponent, CartridgeActivatedEvent>(OnActivate);
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

    private void OnActivate(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeActivatedEvent args)
    {
        if (!TryComp(args.Loader, out PdaComponent? pda) || pda.ContainedId is not { } id)
            return;

        if (TryComp<BankCardComponent>(id, out var bankCardComp) && bankCartridge.Comp.AccountId == null)
        {
            if (bankCardComp.AccountId.HasValue
            && _bankCardSystem.TryGetAccount(bankCardComp.AccountId.Value, out var bankAccount))
            {
                bankCartridge.Comp.AccountId = bankAccount.AccountId;
                bankAccount.CartridgeUid = bankCartridge;
            }
        }
    }

    private void OnNotificationSet(Entity<BankCartridgeComponent> bankCartridge, SetNotificationMessage args)
    {
        bankCartridge.Comp.NotificationOn = !bankCartridge.Comp.NotificationOn;
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

        if (args.AccountTargetId == senderAccount.AccountId)
        {
            bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-error-sender");
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
        {
            UpdateUiState(senderAccount.CartridgeUid.Value, bankCartridge.Comp.Loader!.Value);

            if (!bankCartridge.Comp.NotificationOn || senderAccount.IsBlocked)
                return;

            _cartridgeLoaderSystem.SendNotification(
                bankCartridge.Comp.Loader!.Value,
                Loc.GetString("bank-program-notification-header"),
                Loc.GetString("bank-transfer"));
        }

        if (targetAccount.CartridgeUid != null && targetAccount.CartridgeUid != senderAccount.CartridgeUid)
        {
            if (Comp<CartridgeComponent>(targetAccount.CartridgeUid.Value).LoaderUid is not { } loaderUid
            || !TryComp<BankCartridgeComponent>(targetAccount.CartridgeUid.Value, out var targetCartridge))
                return;

            UpdateUiState(targetAccount.CartridgeUid.Value, loaderUid);

            if (!targetCartridge.NotificationOn || targetAccount.IsBlocked)
                return;

            _cartridgeLoaderSystem.SendNotification(
                loaderUid,
                Loc.GetString("bank-program-notification-header"),
                Loc.GetString("bank-transfer"));
        }

        bankCartridge.Comp.TransferResult = Loc.GetString("bank-program-ui-transfer-success",
            ("amount", args.Amount), ("target", targetAccount.Name));
    }

    private void OnBalanceChanged(Entity<BankCartridgeComponent> ent, ref EconomyBalanceChangedEvent args)
    {
        if (Comp<CartridgeComponent>(ent).LoaderUid is not { } loaderUid)
            return;

        if (!ent.Comp.AccountId.HasValue || !_bankCardSystem.TryGetAccount(ent.Comp.AccountId.Value, out var account)
        || account.IsBlocked)
            return;

        UpdateUiState(ent);

        if (!ent.Comp.NotificationOn)
            return;

        _cartridgeLoaderSystem.SendNotification(
            loaderUid,
            Loc.GetString("bank-program-notification-header"),
            args.OperationType);
    }

    private void OnUiReady(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(bankCartridge);
    }

    private void OnUiMessage(Entity<BankCartridgeComponent> bankCartridge, ref CartridgeMessageEvent args)
    {
        if (args is BankAccountLinkMessage message)
            OnAccountLink(bankCartridge, message);

        if (args is BankTransferMessage transferMessage)
            OnTransfer(bankCartridge, transferMessage);

        if (args is SetNotificationMessage notificationMessage)
            OnNotificationSet(bankCartridge, notificationMessage);

        UpdateUiState(bankCartridge);
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
            state.IsBlocked = account.IsBlocked;
            state.NotificationOn = component.NotificationOn;
        }

        _cartridgeLoaderSystem?.UpdateCartridgeUiState(loaderUid, state);
    }

    public void UpdateUiState(EntityUid cartridgeUid)
    {
        if (!TryComp<BankCartridgeComponent>(cartridgeUid, out var component) || component.Loader == null)
            return;

        UpdateUiState(cartridgeUid, component.Loader.Value, component);
    }
}
