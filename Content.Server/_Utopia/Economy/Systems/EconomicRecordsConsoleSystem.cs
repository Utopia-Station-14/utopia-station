using Content.Server.Station.Systems;
using Content.Shared._Utopia.Economy;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Events;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._Utopia.Economy;

public sealed partial class EconomicRecordsConsoleSystem : EntitySystem
{
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private BankCardSystem _bankCard = default!;
    [Dependency] private StationRecordsSystem _stationRecords = default!;
    [Dependency] private StationRecordKeyStorageSystem _keyStorage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EconomicRecordsConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<EconomicRecordsConsoleComponent, GeneralRecordCreatedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<EconomicRecordsConsoleComponent, RecordRemovedEvent>(UpdateUserInterface);

        Subs.BuiEvents<EconomicRecordsConsoleComponent>(EconomicRecordsConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<EconomicRecordsBlockMessage>(OnBlockMessage);
        });
    }

    private void OnKeySelected(Entity<EconomicRecordsConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent);
    }

    private void OnFiltersChanged(Entity<EconomicRecordsConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null || ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    private void OnBlockMessage(Entity<EconomicRecordsConsoleComponent> ent, ref EconomicRecordsBlockMessage msg)
    {
        var station = _station.GetOwningStation(ent);
        if (station != null && HasComp<StationRecordsComponent>(station))
        {
            if (ent.Comp.ActiveKey is { } keyId)
            {
                var key = new StationRecordKey(keyId, station.Value);

                if (_keyStorage.TryGetEntityWithKey(key, out var idCardUid)
                && idCardUid != null
                && TryComp<BankCardComponent>(idCardUid.Value, out var bankCard)
                && bankCard.AccountId.HasValue
                && _bankCard.TryGetAccount(bankCard.AccountId.Value, out var account))
                {
                    account.IsBlocked = _bankCard.ToggleAccount(account);
                }
            }
        }

        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface<T>(Entity<EconomicRecordsConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    private void UpdateUserInterface(Entity<EconomicRecordsConsoleComponent> ent)
    {
        var station = _station.GetOwningStation(ent);
        Dictionary<uint, string>? listing = null;
        GeneralStationRecord? record = null;

        int? accountId = null;
        var balance = 0;
        var history = new List<TransactionsHistory>();
        var isBlocked = false;

        if (station != null && TryComp<StationRecordsComponent>(station, out var stationRecords))
        {
            listing = _stationRecords.BuildListing((station.Value, stationRecords), ent.Comp.Filter);
            if (ent.Comp.ActiveKey is { } keyId)
            {
                var key = new StationRecordKey(keyId, station.Value);
                _stationRecords.TryGetRecord(key, out record, stationRecords);

                if (_keyStorage.TryGetEntityWithKey(key, out var idCardUid)
                && idCardUid != null
                && TryComp<BankCardComponent>(idCardUid.Value, out var bankCard)
                && bankCard.AccountId.HasValue
                && _bankCard.TryGetAccount(bankCard.AccountId.Value, out var account))
                {
                    accountId = account.AccountId;
                    balance = _bankCard.GetBalance(bankCard.AccountId.Value);
                    history = account.History;
                    isBlocked = account.IsBlocked;
                }
            }
        }

        var state = new EconomicRecordsConsoleState(
            listing,
            ent.Comp.ActiveKey,
            record,
            ent.Comp.Filter,
            accountId,
            balance,
            history,
            isBlocked
        );

        _ui.SetUiState(ent.Owner, EconomicRecordsConsoleKey.Key, state);
    }
}
