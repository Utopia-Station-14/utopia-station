namespace Content.Server._Utopia.ShiftSchedule;

[RegisterComponent]
public sealed partial class ShiftScheduleCardComponent : Component
{
    [DataField]
    public List<ShiftScheduleCardEntry> Entries = new();
}

[DataDefinition]
public sealed partial class ShiftScheduleCardEntry
{
    [DataField(required: true)]
    public string Title = default!;

    [DataField(required: true)]
    public int TimeSeconds;

    [DataField]
    public string Description = string.Empty;
}
