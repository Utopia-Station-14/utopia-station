using Content.Server.Hands.Systems;
using Content.Shared._Utopia.Economy;
using Content.Shared.Access.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Utopia.Economy;

public sealed partial class EftposSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private BankCardSystem _bankCardSystem = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private HandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EftposComponent, EftposLockMessage>(OnLock);
        SubscribeLocalEvent<EftposComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<EftposComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.BankAccountId == null || !TryComp(args.Used, out BankCardComponent? bankCard)
        || bankCard.AccountId == null || bankCard.AccountId == ent.Comp.BankAccountId
        || ent.Comp.Amount <= 0 || bankCard.CommandBudgetCard)
            return;

        if (!_bankCardSystem.TryGetAccount(bankCard.AccountId.Value, out var account) || account.IsBlocked)
        {
            _popupSystem.PopupEntity(Loc.GetString("bank-operation-error"), ent, PopupType.Medium);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
            return;
        }

        if (_bankCardSystem.TryChangeBalance(bankCard.AccountId.Value, -ent.Comp.Amount)
        && _bankCardSystem.TryChangeBalance(ent.Comp.BankAccountId.Value, ent.Comp.Amount))
        {
            _popupSystem.PopupEntity(Loc.GetString("eftpos-transaction-success"), ent);
            _audioSystem.PlayPvs(ent.Comp.SoundApply, ent);
        }
        else
        {
            _popupSystem.PopupEntity(Loc.GetString("bank-operation-error"), ent);
            _audioSystem.PlayPvs(ent.Comp.SoundDeny, ent);
        }
    }

    private void OnLock(Entity<EftposComponent> ent, ref EftposLockMessage args)
    {
        if (!HasComp<HandsComponent>(args.Actor))
            return;

        var held = _handsSystem.GetActiveItem(args.Actor);
        if (held == null || !TryComp<BankCardComponent>(held.Value, out var bankCard))
            return;

        if (ent.Comp.BankAccountId == null)
        {
            ent.Comp.BankAccountId = bankCard.AccountId;
            ent.Comp.Amount = args.Amount;
        }
        else if (ent.Comp.BankAccountId == bankCard.AccountId)
        {
            ent.Comp.BankAccountId = null;
            ent.Comp.Amount = 0;
        }

        UpdateUiState(ent, ent.Comp.BankAccountId != null, ent.Comp.Amount,
            GetOwner(held.Value, ent.Comp.BankAccountId));
    }

    private string GetOwner(EntityUid uid, int? bankAccountId)
    {
        if (bankAccountId == null || !_bankCardSystem.TryGetAccount(bankAccountId.Value, out var account))
            return string.Empty;

        if (TryComp(uid, out IdCardComponent? idCard) && idCard.FullName != null)
            return idCard.FullName;

        return account.Name == string.Empty ? account.AccountId.ToString() : account.Name;
    }

    private void UpdateUiState(EntityUid uid, bool locked, int amount, string owner)
    {
        var state = new EftposBuiState
        {
            Locked = locked,
            Amount = amount,
            Owner = owner
        };

        _ui.SetUiState(uid, EftposKey.Key, state);
    }
}
