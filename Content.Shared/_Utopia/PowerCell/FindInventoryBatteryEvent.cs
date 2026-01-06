using Content.Shared.Inventory;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared._Utopia.PowerCells;
[ByRefEvent]
public record struct FindInventoryBatteryEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;

    public EntityUid? FoundBattery { get; set; }
}