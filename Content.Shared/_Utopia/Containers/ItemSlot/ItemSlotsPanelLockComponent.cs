namespace Content.Shared._Utopia.Containers.ItemSlots;

[RegisterComponent]
public sealed partial class ItemSlotsPanelLockComponent : Component
{
    [DataField(required: true)]
    public List<string> Slots = new();
}
