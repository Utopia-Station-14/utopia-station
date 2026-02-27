using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Shared._Utopia.Economy;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Economy;

public sealed class ATMSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly BankCardSystem _bankCardSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ATMComponent, EntInsertedIntoContainerMessage>(OnCardInserted);
        SubscribeLocalEvent<ATMComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        SubscribeLocalEvent<ATMComponent, ATMRequestWithdrawMessage>(OnWithdrawRequest);
        SubscribeLocalEvent<ATMComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ATMComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<ATMComponent, GotEmaggedEvent>(OnEmag);
    }

    private void OnEmag(Entity<ATMComponent> ent, ref GotEmaggedEvent args)
    {
        args.Handled = true;
    }

    private void OnComponentStartup(Entity<ATMComponent> ent, ref ComponentStartup args)
    {
        UpdateUiState(ent, -1, false, Loc.GetString("atm-ui-insert-card"));
    }

    private void OnInteractUsing(Entity<ATMComponent> ent, ref InteractUsingEvent args)
    {
        if (!_itemSlots.TryGetSlot(ent, ent.Comp.SlotId, out var slot))
            return;

        if (!TryComp<CurrencyComponent>(args.Used, out var currency) || !currency.Price.Keys.Contains(ent.Comp.CurrencyType))
            return;

        if (!slot.Item.HasValue)
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-trying-insert-cash-error"), args.Target, args.User, PopupType.Medium);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
            return;
        }

        var stack = Comp<StackComponent>(args.Used);
        var bankCard = Comp<BankCardComponent>(slot.Item.Value);
        var amount = stack.Count;

        if (_random.Prob(ent.Comp.ErrorChance))
        {
            Del(args.Used);
            args.Handled = true;

            _stackSystem.SpawnAtPosition(amount, _prototypeManager.Index(ent.Comp.CreditStackPrototype),
                Transform(ent).Coordinates);

            _audioSystem.PlayPvs(ent.Comp.SoundWithdrawCurrency, ent);
            _popupSystem.PopupEntity(Loc.GetString("atm-error"), ent);
            return;
        }

        if (!_bankCardSystem.TryChangeBalance(bankCard.AccountId!.Value, amount))
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-deposit-failed"), ent, args.User, PopupType.Medium);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
            return;
        }

        Del(args.Used);
        args.Handled = true;

        _audioSystem.PlayPvs(ent.Comp.SoundInsertCurrency, ent);
        UpdateUiState(ent, _bankCardSystem.GetBalance(bankCard.AccountId.Value), true,
            Loc.GetString("atm-ui-select-withdraw-amount"));
    }

    private void OnCardInserted(Entity<ATMComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<BankCardComponent>(args.Entity, out var bankCard) || !bankCard.AccountId.HasValue)
            return;

        UpdateUiState(ent, _bankCardSystem.GetBalance(bankCard.AccountId.Value), true,
            Loc.GetString("atm-ui-select-withdraw-amount"));
    }

    private void OnCardRemoved(Entity<ATMComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUiState(ent, -1, false, Loc.GetString("atm-ui-insert-card"));
    }

    private void OnWithdrawRequest(Entity<ATMComponent> ent, ref ATMRequestWithdrawMessage args)
    {
        if (!_itemSlots.TryGetSlot(ent, ent.Comp.SlotId, out var slot))
            return;

        if (!TryComp<BankCardComponent>(slot.Item, out var bankCard)
        || !bankCard.AccountId.HasValue)
        {
            if (slot.ContainerSlot != null)
            {
                _container.EmptyContainer(slot.ContainerSlot);
            }

            return;
        }

        if (!_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account)
        || account.AccountPin != args.Pin && !HasComp<EmaggedComponent>(ent))
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-wrong-pin"), ent);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
            return;
        }

        if (!_bankCardSystem.TryChangeBalance(account.AccountId, -args.Amount))
        {
            _popupSystem.PopupEntity(Loc.GetString("atm-not-enough-cash"), ent);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
            return;
        }

        _stackSystem.SpawnAtPosition(args.Amount, _prototypeManager.Index(ent.Comp.CreditStackPrototype),
            Transform(ent).Coordinates);

        _audioSystem.PlayPvs(ent.Comp.SoundWithdrawCurrency, ent);

        UpdateUiState(ent, account.Balance, true, Loc.GetString("atm-ui-select-withdraw-amount"));
    }

    private void UpdateUiState(EntityUid uid, int balance, bool hasCard, string infoMessage)
    {
        var state = new ATMBuiState
        {
            AccountBalance = balance,
            HasCard = hasCard,
            InfoMessage = infoMessage
        };

        _ui.SetUiState(uid, ATMUiKey.Key, state);
    }
}
