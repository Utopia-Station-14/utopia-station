using Content.Shared.StationRecords;

namespace Content.Server._Utopia.Economy;

[RegisterComponent, Access(typeof(EconomicRecordsConsoleSystem))]
public sealed partial class EconomicRecordsConsoleComponent : Component
{
    [ViewVariables]
    public uint? ActiveKey;

    [ViewVariables]
    public StationRecordsFilter? Filter;
}

