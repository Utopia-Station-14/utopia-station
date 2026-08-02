using Robust.Shared.Serialization;

namespace Content.Shared.VendingMachines
{
    [Serializable, NetSerializable]
    public sealed class VendingMachineEjectMessage : BoundUserInterfaceMessage
    {
        public readonly InventoryType Type;
        public readonly string ID;
        public VendingMachineEjectMessage(InventoryType type, string id)
        {
            Type = type;
            ID = id;
        }
    }

    // Utopia-Tweak : Economy
    [Serializable, NetSerializable]
    public sealed class VendingMachineInterfaceState : BoundUserInterfaceState
    {
        public List<VendingMachineInventoryEntry> Inventory;
        public double PriceMultiplier;
        public int Credits;

        public VendingMachineInterfaceState(List<VendingMachineInventoryEntry> inventory, double priceMultiplier,
            int credits)
        {
            Inventory = inventory;
            PriceMultiplier = priceMultiplier;
            Credits = credits;
        }
    }

    [Serializable, NetSerializable]
    public sealed class VendingMachineWithdrawMessage : BoundUserInterfaceMessage;
    // Utopia-Tweak : Economy

    [Serializable, NetSerializable]
    public enum VendingMachineUiKey
    {
        Key,
    }
}
