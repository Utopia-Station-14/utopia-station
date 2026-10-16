using System.Linq;
using System.Numerics;
using Content.Server._Utopia.Economy;
using Content.Server.Access.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Stack;
using Content.Server.VendingMachines.Components;
using Content.Server.Vocalization.Systems;
using Content.Shared._Utopia.Economy;
using Content.Shared.Advertise.Components;
using Content.Shared.Cargo;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.VendingMachines;
using Content.Shared.VendingMachines.Components;
using Content.Shared.Wall;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.VendingMachines;

public sealed partial class VendingMachineSystem : SharedVendingMachineSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private ThrowingSystem _throwingSystem = default!;

    // Utopia-Tweak : Economy
    [Dependency] private BankCardSystem _bankCard = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private StackSystem _stackSystem = default!;
    [Dependency] private TagSystem _tag = default!;
    // Utopia-Tweak : Economy

    private const string IgnoreBalanceCheck = "UtopiaIgnoreBalanceChecks"; // Utopia-Tweak : Economy
    private const float WallVendEjectDistanceFromWall = 1f;

    protected override bool ShouldThrowVendItem(Entity<VendingMachineEjectComponent> entity)
    {
        return HasComp<VendingMachineShootComponent>(entity.Owner);
    }

    // Utopia-Tweak : Economy
    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<VendingMachineComponent>(VendingMachineUiKey.Key, subs =>
        {
            subs.Event<VendingMachineWithdrawMessage>(OnWithdrawMessage);
        });
    }

    public override void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component)
    {
        if (!IsAuthorized(uid, sender, component))
            return;

        if (!TryComp<VendingMachineEjectComponent>(uid, out var ejectComponent))
            return;

        if (ejectComponent.Ejecting || component.Broken)
            return;

        var entry = GetEntry(uid, itemId, type, component);
        if (entry == null)
        {
            Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-invalid-item"), uid, sender);
            Deny((uid, component), sender, ejectComponent);
            return;
        }

        if (entry.Amount <= 0)
        {
            Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid, sender);
            Deny((uid, component), sender, ejectComponent);
            return;
        }

        var price = GetPrice(entry, component);
        var canVendForFree = component.AllForFree || _tag.HasTag(sender, IgnoreBalanceCheck);

        if (price > 0 && !canVendForFree && component.Credits < price && !TryPayWithBankCard(sender, price))
        {
            Popup.PopupEntity(Loc.GetString("vending-machine-component-no-balance"), uid, sender);
            Deny((uid, component), sender, ejectComponent);
            return;
        }

        if (price > 0 && !canVendForFree && component.Credits >= price)
            component.Credits -= price;

        TryEjectVendorItem(uid, type, itemId, ShouldThrowVendItem((uid, ejectComponent)), sender, component, ejectComponent);
    }

    private bool TryPayWithBankCard(EntityUid user, int amount)
    {
        if (!_idCard.TryFindIdCard(user, out var idCard)
            || !TryComp<BankCardComponent>(idCard.Owner, out var bankCard)
            || bankCard.AccountId == null
            || !_bankCard.TryGetAccount(bankCard.AccountId.Value, out var account)
            || account.IsBlocked)
        {
            return false;
        }

        return _bankCard.TryChangeBalance(bankCard.AccountId.Value, -amount);
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(EntityUid uid, VendingMachineComponent component, InteractUsingEvent args)
    {
        if (component.AllForFree || component.Broken || !Receiver.IsPowered(uid))
            return;

        if (!TryComp<CurrencyComponent>(args.Used, out var currency)
            || !currency.Price.ContainsKey(component.CurrencyType)
            || !TryComp<StackComponent>(args.Used, out var stack))
        {
            return;
        }

        component.Credits += stack.Count;
        Del(args.Used);
        Dirty(uid, component);
        UpdateUI((uid, component));
        Audio.PlayPvs(component.SoundInsertCurrency, uid);
        args.Handled = true;
    }

    protected override int GetEntryPrice(EntityPrototype proto)
    {
        var price = (int)_pricing.GetEstimatedPrice(proto);
        return price > 0 ? price : 25;
    }

    private void OnWithdrawMessage(EntityUid uid, VendingMachineComponent component, VendingMachineWithdrawMessage args)
    {
        if (component.Broken || !Receiver.IsPowered(uid))
            return;

        if (!IsAuthorized(uid, args.Actor, component))
            return;

        if (component.Credits <= 0)
        {
            Deny((uid, component), args.Actor);
            return;
        }

        _stackSystem.SpawnAtPosition(component.Credits, ProtoMan.Index(component.CreditStackPrototype),
            Transform(uid).Coordinates);

        component.Credits = 0;
        Dirty(uid, component);
        UpdateUI((uid, component));
        Audio.PlayPvs(component.SoundWithdrawCurrency, uid);
    }
    // Utopia-Tweak : Economy

    protected override void EjectItem(Entity<VendingMachineComponent?, VendingMachineEjectComponent?> entity, bool forceEject = false)
    {
        if (!Resolve(entity.Owner, ref entity.Comp1, ref entity.Comp2))
            return;

        var uid = entity.Owner;
        var ejectComponent = entity.Comp2;

        if (ejectComponent.NextItemToEject is not { } item)
        {
            ejectComponent.ThrowNextItem = false;
            return;
        }

        // Default spawn coordinates
        var xform = Transform(uid);
        var spawnCoordinates = xform.Coordinates;

        //Make sure the wallvends spawn outside of the wall.
        if (TryComp<WallMountComponent>(uid, out var wallMountComponent))
        {
            var offset = (wallMountComponent.Direction + xform.LocalRotation - Math.PI / 2).ToVec() * WallVendEjectDistanceFromWall;
            spawnCoordinates = spawnCoordinates.Offset(offset);
        }

        var ent = Spawn(item, spawnCoordinates);

        if (ejectComponent.ThrowNextItem)
        {
            var range = ejectComponent.NonLimitedEjectRange;
            var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
            _throwingSystem.TryThrow(ent, direction, ejectComponent.NonLimitedEjectForce);
        }

        ejectComponent.NextItemToEject = null;
        ejectComponent.ThrowNextItem = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = Timing.CurTime;
        var dispenseOnHitQuery = EntityQueryEnumerator<VendingMachineDispenseOnHitComponent>();
        while (dispenseOnHitQuery.MoveNext(out _, out var dispenseOnHit))
        {
            if (dispenseOnHit.NextDispenseTime is not { } nextDispenseTime || curTime <= nextDispenseTime)
                continue;

            dispenseOnHit.NextDispenseTime = null;
        }

        var disabled = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent, VendingMachineEjectComponent>();
        while (disabled.MoveNext(out var uid, out _, out var comp, out var eject))
        {
            if (eject.NextEmpEject >= curTime) continue;

            EjectRandom((uid, comp, eject), true, false);
            eject.NextEmpEject += (5 * eject.EjectDelay);
        }
    }

    [SubscribeLocalEvent]
    private void OnVendingPrice(Entity<VendingMachineComponent> entity, ref PriceCalculationEvent args)
    {
        var price = 0.0;

        foreach (var entry in entity.Comp.Inventory.Values)
        {
            if (!ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto))
            {
                Log.Error($"Unable to find entity prototype {entry.ID} on {ToPrettyString(entity)} vending.");
                continue;
            }

            price += entry.Amount * _pricing.GetEstimatedPrice(proto);
        }

        args.Price += price;
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(Entity<VendingMachineComponent> entity, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased && entity.Comp.Broken)
        {
            entity.Comp.Broken = false;
            Dirty(entity);
            return;
        }

        if (!TryComp<VendingMachineDispenseOnHitComponent>(entity.Owner, out var dispenseOnHit))
            return;

        if (entity.Comp.Broken || dispenseOnHit.CoolingDown || args.DamageDelta == null)
            return;

        if (!(args.DamageIncreased && args.DamageDelta.GetTotal() >= dispenseOnHit.Threshold) ||
            !_random.Prob(dispenseOnHit.Chance)) return;

        if (dispenseOnHit.NextDispenseDelay != null)
        {
            dispenseOnHit.NextDispenseTime = Timing.CurTime + dispenseOnHit.NextDispenseDelay.Value;
        }

        EjectRandom((entity.Owner, entity.Comp), throwItem: true, forceEject: true);
    }

    [SubscribeLocalEvent]
    private void OnSelfDispense(Entity<VendingMachineComponent> entity, ref VendingMachineSelfDispenseEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EjectRandom((entity.Owner, entity.Comp), throwItem: true, forceEject: false);
    }

    [SubscribeLocalEvent]
    private void OnPriceCalculation(Entity<VendingMachineRestockComponent> entity, ref PriceCalculationEvent args)
    {
        List<double> priceSets = new();

        // Find the most expensive inventory and use that as the highest price.
        foreach (var vendingInventory in entity.Comp.CanRestock)
        {
            double total = 0;

            if (ProtoMan.TryIndex(vendingInventory, out VendingMachineInventoryPrototype? inventoryPrototype))
            {
                foreach (var (item, amount) in inventoryPrototype.StartingInventory)
                {
                    if (ProtoMan.TryIndex(item, out EntityPrototype? prototype))
                        total += _pricing.GetEstimatedPrice(prototype) * amount;
                }
            }

            priceSets.Add(total);
        }

        args.Price += priceSets.Max();
    }

    [SubscribeLocalEvent]
    private void OnTryVocalize(Entity<VendingMachineComponent> ent, ref TryVocalizeEvent args)
    {
        args.Cancelled |= ent.Comp.Broken;
    }

    public void SetShooting(Entity<VendingMachineEjectComponent?> entity, bool canShoot)
    {
        if (!Resolve(entity.Owner, ref entity.Comp))
            return;

        if (canShoot)
            EnsureComp<VendingMachineShootComponent>(entity.Owner);
        else
            RemComp<VendingMachineShootComponent>(entity.Owner);
    }

    /// <summary>
    /// Sets the <see cref="VendingMachineComponent.Contraband"/> property of the vending machine.
    /// </summary>
    public void SetContraband(Entity<VendingMachineComponent> entity, bool contraband)
    {
        entity.Comp.Contraband = contraband;
        Dirty(entity);
    }

    /// <summary>
    /// Ejects a random item from the available stock. Will do nothing if the vending machine is empty.
    /// </summary>
    /// <param name="entity"></param>
    /// <param name="throwItem">Whether to throw the item in a random direction after dispensing it.</param>
    /// <param name="forceEject">Whether to skip the regular ejection checks and immediately dispense the item without animation.</param>
    public void EjectRandom(
        Entity<VendingMachineComponent?, VendingMachineEjectComponent?> entity,
        bool throwItem,
        bool forceEject = false)
    {
        if (!Resolve(entity.Owner, ref entity.Comp1, ref entity.Comp2))
            return;

        var uid = entity.Owner;
        var vendComponent = entity.Comp1;
        var ejectComponent = entity.Comp2;
        var availableItems = GetAvailableInventory(uid, vendComponent);
        if (availableItems.Count <= 0)
            return;

        var item = _random.Pick(availableItems);

        if (forceEject)
        {
            ejectComponent.NextItemToEject = item.ID;
            ejectComponent.ThrowNextItem = throwItem;
            var entry = GetEntry(uid, item.ID, item.Type, vendComponent);
            if (entry != null)
            {
                entry.Amount--;
                Dirty(uid, vendComponent);
                UpdateUI((uid, vendComponent));
            }

            EjectItem((uid, vendComponent, ejectComponent), forceEject);
        }
        else
        {
            TryEjectVendorItem(uid, item.Type, item.ID, throwItem, user: null, vendComponent: vendComponent, ejectComponent: ejectComponent);
        }
    }
}
