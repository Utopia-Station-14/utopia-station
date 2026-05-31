using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._Utopia.Economy;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.StationRecords;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Utopia.Economy;

public sealed class SalaryConsoleSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly BankCardSystem _bankCard = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly StationRecordKeyStorageSystem _keyStorage = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SalaryConsoleComponent, EntInsertedIntoContainerMessage>(UpdateUiState);
        SubscribeLocalEvent<SalaryConsoleComponent, EntRemovedFromContainerMessage>(UpdateUiState);

        SubscribeLocalEvent<SalaryConsoleComponent, RecordModifiedEvent>(UpdateUiState);
        SubscribeLocalEvent<SalaryConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUiState);
        SubscribeLocalEvent<SalaryConsoleComponent, RecordRemovedEvent>(UpdateUiState);

        Subs.BuiEvents<SalaryConsoleComponent>(SalaryConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUiState);
            subs.Event<SelectStationRecord>(OnSelectRecord);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<SalaryConsoleSendMoneyMessage>(OnSendMoney);
        });
    }

    private void OnSelectRecord(Entity<SalaryConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUiState(ent);
    }

    private void OnFiltersChanged(Entity<SalaryConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUiState(ent);
        }
    }

    private void OnSendMoney(Entity<SalaryConsoleComponent> ent, ref SalaryConsoleSendMoneyMessage msg)
    {
        if (msg.Amount <= 0)
            return;

        if (!_itemSlots.TryGetSlot(ent, SalaryConsoleComponent.BudgetCardSlotId, out var slot))
            return;

        var cardEntity = slot.Item;
        if (cardEntity == null || !TryComp<BankCardComponent>(cardEntity, out var bankCard)
        || !bankCard.CommandBudgetCard || !bankCard.AccountId.HasValue)
            return;

        var station = _station.GetOwningStation(ent.Owner);
        if (station == null || !HasComp<StationRecordsComponent>(station))
            return;

        var key = new StationRecordKey(msg.RecordKey, station.Value);
        if (!_keyStorage.TryGetEntityWithKey(key, out var idCardUid) || idCardUid == null)
            return;

        if (!TryComp<BankCardComponent>(idCardUid.Value, out var targetCard)
        || !targetCard.AccountId.HasValue || targetCard.CommandBudgetCard)
            return;

        var budgetAccountId = bankCard.AccountId.Value;
        var recipientAccountId = targetCard.AccountId.Value;

        if (!_bankCard.TryGetAccount(recipientAccountId, out var recipientAccount) || recipientAccount.IsBlocked)
        {
            _popupSystem.PopupEntity(Loc.GetString("bank-operation-error"), ent, PopupType.Medium);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
            return;
        }

        if (!_bankCard.TryGetAccount(budgetAccountId, out var budgetAccount) || budgetAccount.AccountPin != msg.Pin)
            return;

        if (_bankCard.TryChangeBalance(budgetAccountId, -msg.Amount))
        {
            _bankCard.TryChangeBalance(recipientAccountId, msg.Amount);
        }

        UpdateUiState(ent);
    }

    private void UpdateUiState<T>(Entity<SalaryConsoleComponent> ent, ref T args)
    {
        UpdateUiState(ent);
    }

    private void UpdateUiState(Entity<SalaryConsoleComponent> ent)
    {
        var station = _station.GetOwningStation(ent.Owner);
        Dictionary<uint, string>? listing = null;
        GeneralStationRecord? record = null;

        if (!_itemSlots.TryGetSlot(ent, SalaryConsoleComponent.BudgetCardSlotId, out var slot))
            return;

        if (station != null && TryComp<StationRecordsComponent>(station, out var stationRecords))
        {
            listing = _stationRecords.BuildListing((station.Value, stationRecords), ent.Comp.Filter);
            if (ent.Comp.ActiveKey is { } keyId)
            {
                var key = new StationRecordKey(keyId, station.Value);
                _stationRecords.TryGetRecord(key, out record, stationRecords);
            }
        }

        var budgetCardInserted = false;
        var budgetCardBalance = 0;
        string? budgetCardLabel = null;

        if (slot.Item is { } card && TryComp<BankCardComponent>(card, out var bankCard)
        && bankCard.CommandBudgetCard && bankCard.AccountId.HasValue
        && _bankCard.TryGetAccount(bankCard.AccountId.Value, out var account))
        {
            budgetCardInserted = true;
            budgetCardBalance = _bankCard.GetBalance(bankCard.AccountId.Value);
            budgetCardLabel = account.Name ?? Loc.GetString("salary-console-budget-card");
        }

        var state = new SalaryConsoleUserInterfaceState(
            listing,
            ent.Comp.ActiveKey,
            record,
            ent.Comp.Filter,
            budgetCardInserted,
            budgetCardBalance,
            budgetCardLabel
        );

        _ui.SetUiState(ent.Owner, SalaryConsoleUiKey.Key, state);
    }
}
