namespace Content.Server._Utopia.ShiftSchedule;

[RegisterComponent]
public sealed partial class ShiftScheduleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public readonly List<ShiftScheduleEntry> Entries = new();
    public int NextId = 1;
    public bool Enabled = true;
}

public sealed class ShiftScheduleEntry(int id, string title, int timeSeconds, string description)
{
    public int Id = id;
    public string Title = title;
    public int TimeSeconds = timeSeconds;
    public string Description = description;
    public bool Announced;
}
