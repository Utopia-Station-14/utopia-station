using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Stains.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class StainBlockerComponent : Component
{
    [DataField("slots", required: true)]
    public SlotFlags BlockedSlots;
}
