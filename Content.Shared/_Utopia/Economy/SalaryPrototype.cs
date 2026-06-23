using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Economy;

[Prototype]
public sealed partial class SalaryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<string, SalaryEntry> Salaries = new();
}

[DataDefinition, Serializable]
public partial struct SalaryEntry
{
    [DataField]
    public int? Roundstart { get; set; } = 0;

    [DataField]
    public int? Salary { get; set; } = 0;
}
