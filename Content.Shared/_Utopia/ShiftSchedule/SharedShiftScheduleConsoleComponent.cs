using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ShiftSchedule;

[Serializable, NetSerializable]
public sealed class ShiftScheduleConsoleInterfaceState(bool canEdit, bool enabled, ShiftScheduleEntryState[] entries, string? status = null, bool clear = false) : BoundUserInterfaceState
{
    public bool CanEdit = canEdit;
    public bool Enabled = enabled;
    public ShiftScheduleEntryState[] Entries = entries;
    public string? Status = status;
    public bool Clear = clear;
}

[Serializable, NetSerializable]
public sealed class ShiftScheduleEntryState(int id, string title, int time, string desc)
{
    public int Id = id;
    public string Title = title;
    public int Time = time;
    public string Description = desc;
}

[Serializable, NetSerializable]
public sealed class ShiftScheduleRequestMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class ShiftScheduleSaveMessage(int? id, string title, int time, string desc) : BoundUserInterfaceMessage
{
    public int? Id = id;
    public string Title = title;
    public int Time = time;
    public string Description = desc;
}

[Serializable, NetSerializable]
public sealed class ShiftScheduleRemoveMessage(int id) : BoundUserInterfaceMessage
{
    public int Id = id;
}

[Serializable, NetSerializable]
public sealed class ShiftScheduleToggleMessage(bool enabled) : BoundUserInterfaceMessage
{
    public bool Enabled = enabled;
}

[Serializable, NetSerializable]
public sealed class ShiftScheduleAnnounceMessage(int id) : BoundUserInterfaceMessage
{
    public int Id = id;
}

[Serializable, NetSerializable]
public sealed class ShiftScheduleImportCodeMessage(string code) : BoundUserInterfaceMessage
{
    public string Code = code;
}

[Serializable, NetSerializable]
public enum ShiftScheduleConsoleUiKey
{
    Key
}
