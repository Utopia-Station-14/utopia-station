using Content.Shared.Chemistry.Components;
using Content.Shared.Inventory;

namespace Content.Shared._Utopia.Fluids;

public sealed class SpilledOnEvent(EntityUid source, Solution solution) : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;

    public EntityUid Source = source;

    public Solution Solution = solution;
}
