using Content.Shared.StationRecords;

namespace Content.Shared._Utopia.Economy.Components;

[RegisterComponent]
public sealed partial class EconomicRecordsConsoleComponent : Component
{
    [ViewVariables]
    public uint? ActiveKey;

    [ViewVariables]
    public StationRecordsFilter? Filter;
}
