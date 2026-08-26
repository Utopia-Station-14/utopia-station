using System.Linq;
using System.Numerics;
using Content.Server._Utopia.Economy;
using Content.Server.Access.Systems;
using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Server.Stack;
using Content.Server.Store.Components;
using Content.Server.Vocalization.Systems;
using Content.Shared._Utopia.Economy;
using Content.Shared.Advertise.Components;
using Content.Shared.Cargo;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.VendingMachines;
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
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private StackSystem _stackSystem = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    // Utopia-Tweak : Economy

    private const string IgnoreBalanceCheck = "UtopiaIgnoreBalanceChecks"; // Utopia-Tweak : Economy
    private const float WallVendEjectDistanceFromWall = 1f;

    [SubscribeLocalEvent]
    private void OnVendingPrice(EntityUid uid, VendingMachineComponent component, ref PriceCalculationEvent args)
    {
        var price = 0.0;

        foreach (var entry in component.Inventory.Values)
        {
            if (!ProtoMan.TryIndex<EntityPrototype>(entry.ID, out var proto))
            {
                Log.Error($"Unable to find entity prototype {entry.ID} on {ToPrettyString(uid)} vending.");
                continue;
            }

            price += entry.Amount * _pricing.GetEstimatedPrice(proto);
        }

        args.Price += price;
    }

    protected override void OnMapInit(EntityUid uid, VendingMachineComponent component, MapInitEvent args)
    {
        base.OnMapInit(uid, component, args);

        if (HasComp<ApcPowerReceiverComponent>(uid))
        {
            TryUpdateVisualState((uid, component));
        }
    }

    [SubscribeLocalEvent]
    private void OnPowerChanged(EntityUid uid, VendingMachineComponent component, ref PowerChangedEvent args)
    {
        TryUpdateVisualState((uid, component));
    }

    [SubscribeLocalEvent]
    private void OnDamageChanged(EntityUid uid, VendingMachineComponent component, DamageChangedEvent args)
    {
        if (!args.DamageIncreased && component.Broken)
        {
            component.Broken = false;
            Dirty(uid, component);
            TryUpdateVisualState((uid, component));
            return;
        }

        if (component.Broken || component.DispenseOnHitCoolingDown ||
            component.DispenseOnHitChance == null || args.DamageDelta == null)
            return;

        if (args.DamageIncreased && args.DamageDelta.GetTotal() >= component.DispenseOnHitThreshold &&
            _random.Prob(component.DispenseOnHitChance.Value))
        {
            if (component.DispenseOnHitCooldown != null)
            {
                component.DispenseOnHitEnd = Timing.CurTime + component.DispenseOnHitCooldown.Value;
            }

            EjectRandom(uid, throwItem: true, forceEject: true, component);
        }

    }

    // Utopia-Tweak : Economy
    public override void AuthorizedVend(EntityUid uid, EntityUid sender, InventoryType type, string itemId, VendingMachineComponent component)
    {
        if (component.Ejecting || !IsAuthorized(uid, sender, component))
            return;

        var entry = GetEntry(uid, itemId, type, component);
        if (entry == null)
            return;

        if (entry.Amount <= 0)
        {
            Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid);
            Deny((uid, component));
            return;
        }

        var price = GetPrice(entry, component);
        var canVendForFree = component.AllForFree || _tag.HasTag(sender, IgnoreBalanceCheck);

        if (price <= 0 || canVendForFree)
        {
            TryEjectVendorItem(uid, type, itemId, component.CanShoot, sender, component);
            return;
        }

        if (component.Credits >= price)
        {
            component.Credits -= price;
            TryEjectVendorItem(uid, type, itemId, component.CanShoot, sender, component);
            return;
        }
        else if (TryPayWithBankCard(sender, price))
        {
            TryEjectVendorItem(uid, type, itemId, component.CanShoot, sender, component);
            return;
        }

        Popup.PopupEntity(Loc.GetString("vending-machine-component-no-balance"), uid);
        Deny((uid, component));
    }

    public override void TryEjectVendorItem(EntityUid uid, InventoryType type, string itemId, bool throwItem, EntityUid? user = null, VendingMachineComponent? vendComponent = null)
    {
        if (!Resolve(uid, ref vendComponent))
            return;

        if (vendComponent.Ejecting || vendComponent.Broken || !Receiver.IsPowered(uid))
            return;

        var entry = GetEntry(uid, itemId, type, vendComponent);

        if (string.IsNullOrEmpty(entry?.ID))
        {
            Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-invalid-item"), uid);
            Deny((uid, vendComponent));
            return;
        }

        if (entry.Amount <= 0)
        {
            Popup.PopupEntity(Loc.GetString("vending-machine-component-try-eject-out-of-stock"), uid);
            Deny((uid, vendComponent));
            return;
        }

        vendComponent.EjectEnd = Timing.CurTime + vendComponent.EjectDelay;
        vendComponent.NextItemToEject = entry.ID;
        vendComponent.ThrowNextItem = throwItem;

        if (TryComp(uid, out SpeakOnUIClosedComponent? speakComponent))
            SpeakOn.TrySetFlag((uid, speakComponent));

        entry.Amount--;
        Dirty(uid, vendComponent);
        UpdateUI((uid, vendComponent));
        TryUpdateVisualState((uid, vendComponent));
        Audio.PlayPvs(vendComponent.SoundVend, uid);
    }

    private bool TryPayWithBankCard(EntityUid user, int amount)
    {
        if (!_idCard.TryFindIdCard(user, out var idCard))
            return false;

        if (!TryComp<BankCardComponent>(idCard.Owner, out var bankCard) || bankCard.AccountId == null)
            return false;

        if (!_bankCard.TryGetAccount(bankCard.AccountId.Value, out var account) || account.IsBlocked)
            return false;

        return _bankCard.TryChangeBalance(bankCard.AccountId.Value, -amount);
    }

    [SubscribeLocalEvent]
    private void OnInteractUsing(EntityUid uid, VendingMachineComponent component, InteractUsingEvent args)
    {
        if (component.AllForFree)
            return;

        if (component.Broken || !Receiver.IsPowered(uid))
            return;

        if (!TryComp<CurrencyComponent>(args.Used, out var currency)
        || !currency.Price.ContainsKey(component.CurrencyType))
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack))
            return;

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

    [SubscribeLocalEvent]
    private void OnWithdrawMessage(EntityUid uid, VendingMachineComponent component, VendingMachineWithdrawMessage args)
    {
        if (component.Credits == 0)
        {
            Deny((uid, component), args.Actor);
            return;
        }

        if (!IsAuthorized(uid, args.Actor, component))
            return;

        _stackSystem.SpawnAtPosition(component.Credits, ProtoMan.Index(component.CreditStackPrototype),
            Transform(uid).Coordinates);

        component.Credits = 0;
        Audio.PlayPvs(component.SoundWithdrawCurrency, uid);
        UpdateUI((uid, component));
        Dirty(uid, component);
    }
    // Utopia-Tweak : Economy

    [SubscribeLocalEvent]
    private void OnSelfDispense(EntityUid uid, VendingMachineComponent component, VendingMachineSelfDispenseEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EjectRandom(uid, throwItem: true, forceEject: false, component);
    }

    /// <summary>
    /// Sets the <see cref="VendingMachineComponent.CanShoot"/> property of the vending machine.
    /// </summary>
    public void SetShooting(EntityUid uid, bool canShoot, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.CanShoot = canShoot;
    }

    /// <summary>
    /// Sets the <see cref="VendingMachineComponent.Contraband"/> property of the vending machine.
    /// </summary>
    public void SetContraband(EntityUid uid, bool contraband, VendingMachineComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Contraband = contraband;
        Dirty(uid, component);
    }

    /// <summary>
    /// Ejects a random item from the available stock. Will do nothing if the vending machine is empty.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="throwItem">Whether to throw the item in a random direction after dispensing it.</param>
    /// <param name="forceEject">Whether to skip the regular ejection checks and immediately dispense the item without animation.</param>
    /// <param name="vendComponent"></param>
    public void EjectRandom(EntityUid uid, bool throwItem, bool forceEject = false, VendingMachineComponent? vendComponent = null)
    {
        if (!Resolve(uid, ref vendComponent))
            return;

        var availableItems = GetAvailableInventory(uid, vendComponent);
        if (availableItems.Count <= 0)
            return;

        var item = _random.Pick(availableItems);

        if (forceEject)
        {
            vendComponent.NextItemToEject = item.ID;
            vendComponent.ThrowNextItem = throwItem;
            var entry = GetEntry(uid, item.ID, item.Type, vendComponent);
            if (entry != null)
                entry.Amount--;
            EjectItem(uid, vendComponent, forceEject);
        }
        else
        {
            TryEjectVendorItem(uid, item.Type, item.ID, throwItem, user: null, vendComponent: vendComponent);
        }
    }

    protected override void EjectItem(EntityUid uid, VendingMachineComponent? vendComponent = null, bool forceEject = false)
    {
        if (!Resolve(uid, ref vendComponent))
            return;

        // No need to update the visual state because we never changed it during a forced eject
        if (!forceEject)
            TryUpdateVisualState((uid, vendComponent));

        if (string.IsNullOrEmpty(vendComponent.NextItemToEject))
        {
            vendComponent.ThrowNextItem = false;
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

        var ent = Spawn(vendComponent.NextItemToEject, spawnCoordinates);

        if (vendComponent.ThrowNextItem)
        {
            var range = vendComponent.NonLimitedEjectRange;
            var direction = new Vector2(_random.NextFloat(-range, range), _random.NextFloat(-range, range));
            _throwingSystem.TryThrow(ent, direction, vendComponent.NonLimitedEjectForce);
        }

        vendComponent.NextItemToEject = null;
        vendComponent.ThrowNextItem = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var disabled = EntityQueryEnumerator<EmpDisabledComponent, VendingMachineComponent>();
        while (disabled.MoveNext(out var uid, out _, out var comp))
        {
            if (comp.NextEmpEject < Timing.CurTime)
            {
                EjectRandom(uid, true, false, comp);
                comp.NextEmpEject += (5 * comp.EjectDelay);
            }
        }
    }

    [SubscribeLocalEvent]
    private void OnPriceCalculation(EntityUid uid, VendingMachineRestockComponent component, ref PriceCalculationEvent args)
    {
        List<double> priceSets = new();

        // Find the most expensive inventory and use that as the highest price.
        foreach (var vendingInventory in component.CanRestock)
        {
            double total = 0;

            if (ProtoMan.TryIndex(vendingInventory, out VendingMachineInventoryPrototype? inventoryPrototype))
            {
                foreach (var (item, amount) in inventoryPrototype.StartingInventory)
                {
                    if (ProtoMan.TryIndex(item, out EntityPrototype? entity))
                        total += _pricing.GetEstimatedPrice(entity) * amount;
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
}
