using Content.Shared._Utopia.Power.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Containers;

namespace Content.Server._Utopia.Power;

public sealed partial class BatterySlotVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BatteryComponent, ChargeChangedEvent>(OnBatteryChargeChanged);
        SubscribeLocalEvent<BatterySlotVisualsComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<BatterySlotVisualsComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnBatteryChargeChanged(Entity<BatteryComponent> ent, ref ChargeChangedEvent args)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (!TryComp<PowerCellSlotComponent>(container.Owner, out var slotComp))
            return;

        if (!TryComp<BatterySlotVisualsComponent>(container.Owner, out var visualComp))
            return;

        if (container.ID != slotComp.CellSlotId)
            return;

        UpdateAppearance((container.Owner, visualComp), ent);
    }

    private void OnInserted(Entity<BatterySlotVisualsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<PowerCellSlotComponent>(ent, out var slotComp))
            return;

        if (args.Container.ID != slotComp.CellSlotId)
            return;

        if (!TryComp<BatteryComponent>(args.Entity, out var battery))
            return;

        UpdateAppearance(ent, (args.Entity, battery));
    }

    private void OnRemoved(Entity<BatterySlotVisualsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!TryComp<PowerCellSlotComponent>(ent, out var slotComp))
            return;

        if (args.Container.ID != slotComp.CellSlotId)
            return;

        _appearance.SetData(ent.Owner, BatterySlotVisuals.Battery, false);
    }

    private void UpdateAppearance(Entity<BatterySlotVisualsComponent> ent, Entity<BatteryComponent> battery)
    {
        var currentLevel = (int)_battery.GetCharge(battery.AsNullable());
        var maxCharge = (int)battery.Comp.MaxCharge;

        _appearance.SetData(ent, BatterySlotVisuals.Battery, true);
        _appearance.SetData(ent, BatterySlotVisuals.MaxCharge, maxCharge);
        _appearance.SetData(ent, BatterySlotVisuals.CurrentCharge, currentLevel);
    }
}
