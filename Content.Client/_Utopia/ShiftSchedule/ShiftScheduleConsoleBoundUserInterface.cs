using JetBrains.Annotations;
using Content.Shared._Utopia.ShiftSchedule;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.ShiftSchedule;

[UsedImplicitly]
public sealed class ShiftScheduleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ShiftScheduleConsoleMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ShiftScheduleConsoleMenu>();

        _menu.OnSave += SaveEntry;
        _menu.OnRemove += RemoveEntry;
        _menu.OnToggle += ToggleSchedule;
        _menu.OnAnnounce += AnnounceEntry;
        _menu.OnImportCode += ImportCode;

        SendMessage(new ShiftScheduleRequestMessage());
    }

    private void SaveEntry(int? id, string title, int time, string desc)
    {
        SendMessage(new ShiftScheduleSaveMessage(id, title, time, desc));
    }

    private void RemoveEntry(int id)
    {
        SendMessage(new ShiftScheduleRemoveMessage(id));
    }

    private void ToggleSchedule(bool enabled)
    {
        SendMessage(new ShiftScheduleToggleMessage(enabled));
    }

    private void AnnounceEntry(int id)
    {
        SendMessage(new ShiftScheduleAnnounceMessage(id));
    }

    private void ImportCode(string code)
    {
        SendMessage(new ShiftScheduleImportCodeMessage(code));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ShiftScheduleConsoleInterfaceState scheduleState)
        {
            _menu?.UpdateState(scheduleState);
        }
    }
}
