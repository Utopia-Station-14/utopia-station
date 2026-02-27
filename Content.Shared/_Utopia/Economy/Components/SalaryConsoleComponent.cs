using Content.Shared.StationRecords;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SalaryConsoleComponent : Component
{
    public const string BudgetCardSlotId = "BankCardSlot";

    [ViewVariables(VVAccess.ReadOnly), AutoNetworkedField]
    public uint? ActiveKey;

    [DataField]
    public StationRecordsFilter? Filter;
}

[Serializable, NetSerializable]
public enum SalaryConsoleUiKey
{
    Key
}
