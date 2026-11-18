using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Construction.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceLinking.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Kitchen.Components;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
using Content.Shared.Stacks;
using Content.Shared.Temperature.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Kitchen.EntitySystems;

/// <summary>
/// A system that handles microwave logic, such as activation, malfunctions, and producing cooked recipes.
/// TODO: Replace with a more sophisticated(?) cooking system.
/// </summary>
public abstract partial class MicrowaveSystem : EntitySystem
{
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedSolutionContainerSystem Solution = default!;

    [Dependency] private INetManager _net = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private RecipeSystem _recipes = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedPowerStateSystem _powerState = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedTemperatureSystem _temperature = default!;
    [Dependency] private SharedBatterySystem _battery = default!; // Utopia-Tweak : Microwave
    [Dependency] private ItemSlotsSystem _itemSlots = default!; // Utopia-Tweak : Microwave

    [Dependency] protected EntityQuery<MicrowaveComponent> MicrowaveQuery;
    [Dependency] protected EntityQuery<ActiveMicrowaveComponent> ActiveMicrowaveQuery;
    [Dependency] protected EntityQuery<ItemComponent> ItemQuery;
    [Dependency] protected EntityQuery<StackComponent> StackQuery;

    public override void Initialize()
    {
        base.Initialize();

        InitializeUI();
    }

    // Utopia-Tweak : Microwave
    [SubscribeLocalEvent]
    private void OnBuiOpened(Entity<MicrowaveComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (HasComp<ActiveMicrowaveComponent>(ent) || ent.Comp.Broken)
            return;

        SetAppearance(ent, null, opened: true);
    }

    [SubscribeLocalEvent]
    private void OnBuiClosed(Entity<MicrowaveComponent> ent, ref BoundUIClosedEvent args)
    {
        SetAppearance(ent, null, opened: false);
    }

    private bool HasChargedBattery(EntityUid uid)
    {
        if (!TryComp<PowerCellSlotComponent>(uid, out var cellSlotComp))
            return false;

        var item = _itemSlots.GetItemOrNull(uid, cellSlotComp.CellSlotId);
        if (item == null)
            return false;

        return _battery.GetCharge(item.Value).Charge > 0f;
    }
    // Utopia-Tweak : Microwave

    /// <summary>
    /// Processes every active microwave's ongoing cooking operation.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = Timing.CurTime;

        var query = EntityQueryEnumerator<ActiveMicrowaveComponent, MicrowaveComponent>();
        while (query.MoveNext(out var uid, out var active, out var microwave))
        {

            // Utopia-Tweak : MicrowaveBattery
            if (TryComp<MicrowaveBatteryComponent>(uid, out var microwaveBattery)
            && TryComp<PowerCellSlotComponent>(uid, out var cellSlotComp) && !microwaveBattery.NetworkPower)
            {
                var item = _itemSlots.GetItemOrNull(uid, cellSlotComp.CellSlotId);
                if (item == null || !_battery.TryUseCharge(item.Value, microwaveBattery.CookingPowerDraw * frameTime))
                {
                    StopCooking((uid, microwave));
                    Container.EmptyContainer(microwave.Storage);
                    UpdateUI((uid, microwave));
                    continue;
                }
            }
            // Utopia-Tweak : MicrowaveBattery

            // Roll malfunctions
            if (active.Malfunctioning && active.NextMalfunction < curTime)
            {
                active.NextMalfunction += microwave.MalfunctionInterval;
                DirtyField(uid, active, nameof(active.NextMalfunction));

                RollMalfunction((uid, microwave));
            }

            var timeSinceUpdate = curTime - active.LastCookUpdated;
            var timeElapsed = (float)timeSinceUpdate.TotalSeconds;

            // Finish cooking
            if (active.CookTimeEnd < curTime)
            {
                AddTemperature((uid, microwave), timeElapsed);
                CompleteCooking((uid, active, microwave));
                continue;
            }

            // Otherwise, process the cooking cycle
            if (active.NextCookUpdate > curTime)
                continue;

            active.NextCookUpdate += microwave.UpdateInterval;
            active.LastCookUpdated = curTime;
            DirtyFields(uid, active, null, nameof(active.NextCookUpdate), nameof(active.LastCookUpdated));
            AddTemperature((uid, microwave), timeElapsed);
        }
    }

    /// <summary>
    /// Adds an "on" port to this microwave.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnMapInit(Entity<MicrowaveComponent> ent, ref MapInitEvent args)
    {
        _deviceLink.EnsureSinkPorts(ent, ent.Comp.OnPort);
    }

    /// <summary>
    /// When a microwave is broken, its appearance changes and it stops being usable for cooking.
    /// It will stop any ongoing cooking operations and empty its contents.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnBreak(Entity<MicrowaveComponent> ent, ref BreakageEventArgs args)
    {
        ent.Comp.Broken = true;
        DirtyField(ent.AsNullable(), nameof(ent.Comp.Broken));
        SetAppearance(ent, MicrowaveVisualState.Broken);

        StopCooking(ent);
        Container.EmptyContainer(ent.Comp.Storage);
        UpdateUI(ent.AsNullable());
    }

    /// <summary>
    /// Stop cooking if the microwave loses power.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnPowerChanged(Entity<MicrowaveComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
        {
            SetAppearance(ent, MicrowaveVisualState.Idle);
            StopCooking(ent);
        }

        UpdateUI(ent.AsNullable());
    }

    /// <summary>
    /// Empty the microwave if it is unanchored.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnAnchorChanged(Entity<MicrowaveComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            Container.EmptyContainer(ent.Comp.Storage);
    }

    /// <summary>
    /// Turns the microwave on if its "on" port is activated.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnSignalReceived(Entity<MicrowaveComponent> ent, ref SignalReceivedEvent args)
    {
        // Utopia-Tweak : MicrowaveBattery
        var isPowered = TryComp<MicrowaveBatteryComponent>(ent, out var microwaveBattery)
        && !microwaveBattery.NetworkPower
            ? HasChargedBattery(ent)
            : _power.IsPowered(ent.Owner);
        // Utopia-Tweak : MicrowaveBattery

        if (ent.Comp.Broken || !isPowered) // Utopia-Tweak : Microwave
            return;

        if (args.Port == ent.Comp.OnPort)
            StartCooking(ent, null);
    }

    /// <summary>
    /// Updates the microwave's appearance state.
    /// </summary>
    /// <param name="ent">The microwave entity.</param>
    /// <param name="state">The visual state of the microwave.</param>
    private void SetAppearance(Entity<MicrowaveComponent> ent,
        MicrowaveVisualState? state = null, bool? opened = null) // Utopia-Tweak : Microwave
    {
        // Utopia-Tweak : Microwave
        if (opened != null)
        {
            var openedState = opened.Value ? OpenableMicrowave.Opened : OpenableMicrowave.Closed;
            Appearance.SetData(ent.Owner, PowerDeviceVisuals.VisualState, openedState);
        }

        if (state == null)
            return;
        // Utopia-Tweak : Microwave

        var display = ent.Comp.Broken ? MicrowaveVisualState.Broken : state;
        Appearance.SetData(ent.Owner,
            PowerDeviceVisuals.VisualState,
            display);
    }

    // Utopia-Tweak : Machine Parts
    [SubscribeLocalEvent]
    private void OnRefreshParts(Entity<MicrowaveComponent> ent, ref RefreshPartsEvent args)
    {
        var cookTier = args.PartTiers[ent.Comp.MachinePartCookTimeMultiplier];
        ent.Comp.FinalCookTimeMultiplier = ent.Comp.CookTimeMultiplier * MathF.Pow(ent.Comp.CookTimeScalingConstant, cookTier - 1);
    }

    [SubscribeLocalEvent]
    private void OnUpgradeExamine(Entity<MicrowaveComponent> ent, ref UpgradeExamineEvent args)
    {
        args.AddPercentageUpgrade("microwave-component-upgrade-cook-time", ent.Comp.FinalCookTimeMultiplier);
    }
    // Utopia-Tweak : Machine Parts
}

[Serializable, NetSerializable]
public enum MicrowaveVisualState
{
    Idle,
    Cooking,
    Broken,
    Bloody
}

[NetSerializable, Serializable]
public enum MicrowaveUiKey
{
    Key
}
