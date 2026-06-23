using Content.Shared.Containers.ItemSlots;
using Content.Shared.Wires;

namespace Content.Shared._Utopia.Containers.ItemSlots;

public sealed class ItemSlotsPanelLockSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemSlotsPanelLockComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ItemSlotsPanelLockComponent, PanelChangedEvent>(OnPanelChanged);
    }

    private void OnStartup(Entity<ItemSlotsPanelLockComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<WiresPanelComponent>(ent.Owner, out var panel))
            return;

        SetLocks(ent, !panel.Open);
    }

    private void OnPanelChanged(Entity<ItemSlotsPanelLockComponent> ent, ref PanelChangedEvent args)
    {
        SetLocks(ent, !args.Open);
    }

    private void SetLocks(Entity<ItemSlotsPanelLockComponent> ent, bool locked)
    {
        foreach (var slotName in ent.Comp.Slots)
        {
            if (!_itemSlots.TryGetSlot(ent.Owner, slotName, out var slot))
                continue;

            _itemSlots.SetLock(ent.Owner, slot, locked);
        }
    }
}
