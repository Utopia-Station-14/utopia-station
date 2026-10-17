using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Utopia.ShiftSchedule;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;

namespace Content.Server._Utopia.ShiftSchedule;

public sealed partial class ShiftScheduleConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Update(float frameTime)
    {
        if (_ticker.RunLevel != GameRunLevel.InRound)
            return;

        var elapsed = _ticker.RoundDuration();
        var query = EntityQueryEnumerator<ShiftScheduleComponent>();
        while (query.MoveNext(out _, out var schedule))
        {
            for (var index = 0; index < schedule.Entries.Count;)
            {
                var entry = schedule.Entries[index];
                var isDue = TimeSpan.FromSeconds(entry.TimeSeconds) <= elapsed;

                if (!schedule.Enabled)
                {
                    entry.Announced |= isDue;
                    index++;
                    continue;
                }

                if (entry.Announced || !isDue)
                {
                    index++;
                    continue;
                }

                Announce(schedule, entry);
                schedule.Entries.RemoveAt(index);
            }
        }

        base.Update(frameTime);
    }

    [SubscribeLocalEvent]
    private void OnRequest(EntityUid uid, ShiftScheduleConsoleComponent component, ShiftScheduleRequestMessage message)
    {
        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        var canEdit = message.Actor is { Valid: true } user && (!TryComp<AccessReaderComponent>(uid, out var accessReader) ||
            _accessReader.IsAllowed(user, uid, accessReader));

        UpdateInterface(uid, schedule, canEdit, canEdit ? null : Loc.GetString("shift-schedule-no-access"));
    }

    [SubscribeLocalEvent]
    private void OnSave(EntityUid uid, ShiftScheduleConsoleComponent component, ShiftScheduleSaveMessage message)
    {
        if (message.Actor is not { Valid: true } user)
            return;

        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        if (TryComp<AccessReaderComponent>(uid, out var accessReader)
        && !_accessReader.IsAllowed(user, uid, accessReader))
        {
            UpdateInterface(uid, schedule, false, Loc.GetString("shift-schedule-no-access"));
            return;
        }

        if (message.Title is null || message.Description is null)
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-invalid-entry"));
            return;
        }

        var title = message.Title.Trim();
        var description = message.Description.Trim();

        if (title.Length == 0 || title.Length > ShiftScheduleCode.MaxTitleLength
        || description.Length > ShiftScheduleCode.MaxDescriptionLength
        || message.Time < 0 || message.Time > ShiftScheduleCode.MaxTimeSeconds)
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-invalid-entry"));
            return;
        }

        ShiftScheduleEntry entry;
        if (message.Id is { } id)
        {
            var existingEntry = schedule.Entries.FirstOrDefault(с => с.Id == id);
            if (existingEntry == null)
            {
                UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-entry-missing"));
                return;
            }

            entry = existingEntry;
            entry.Title = title;
            entry.TimeSeconds = message.Time;
            entry.Description = description;
        }
        else
        {
            if (schedule.Entries.Count >= ShiftScheduleCode.MaxEntries)
            {
                UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-limit-reached"));
                return;
            }

            entry = new ShiftScheduleEntry(schedule.NextId++, title, message.Time, description);
            schedule.Entries.Add(entry);
        }

        entry.Announced = _ticker.RunLevel == GameRunLevel.InRound &&
            TimeSpan.FromSeconds(entry.TimeSeconds) <= _ticker.RoundDuration();

        schedule.Entries.Sort(static (left, right) =>
        {
            var timeOrder = left.TimeSeconds.CompareTo(right.TimeSeconds);
            return timeOrder != 0 ? timeOrder : left.Id.CompareTo(right.Id);
        });

        UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-saved"), clear: true);
    }

    [SubscribeLocalEvent]
    private void OnImportCode(EntityUid uid, ShiftScheduleConsoleComponent component, ShiftScheduleImportCodeMessage message)
    {
        if (message.Actor is not { Valid: true } user)
            return;

        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        if (TryComp<AccessReaderComponent>(uid, out var accessReader)
        && !_accessReader.IsAllowed(user, uid, accessReader))
        {
            UpdateInterface(uid, schedule, false, Loc.GetString("shift-schedule-no-access"));
            return;
        }

        if (message.Code is null || !ShiftScheduleCode.TryDecode(message.Code, out var entries)
        || !TryApplySchedule(schedule, schedule.Enabled, entries))
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-code-invalid"));
            return;
        }

        UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-code-imported"), clear: true);
    }

    [SubscribeLocalEvent]
    private void OnCardInsertAttempt(EntityUid uid, ShiftScheduleConsoleComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ShiftScheduleConsoleComponent.CardSlotId)
            return;

        if (args.User is not { } user || !HasComp<ShiftScheduleCardComponent>(args.Item)
        || TryComp<AccessReaderComponent>(uid, out var accessReader) && !_accessReader.IsAllowed(user, uid, accessReader))
        {
            args.Cancelled = true;
        }
    }

    [SubscribeLocalEvent]
    private void OnCardInserted(EntityUid uid, ShiftScheduleConsoleComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ShiftScheduleConsoleComponent.CardSlotId
        || !TryComp<ShiftScheduleCardComponent>(args.Entity, out var card))
            return;

        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        if (card.Entries is null || card.Entries.Any(entry => entry is null))
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-card-invalid"));
            return;
        }

        var entries = card.Entries
            .Select(entry => new ShiftScheduleCodeEntry(entry.Title, entry.TimeSeconds, entry.Description))
            .ToList();

        if (!TryApplySchedule(schedule, true, entries))
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-card-invalid"));
            return;
        }

        QueueDel(args.Entity);
        UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-card-applied"), clear: true);
    }

    private bool TryApplySchedule(ShiftScheduleComponent schedule, bool enabled, List<ShiftScheduleCodeEntry> sourse)
    {
        if (sourse.Count > ShiftScheduleCode.MaxEntries)
            return false;

        foreach (var entry in sourse)
        {
            if (entry.Title is null || entry.Description is null
            || entry.Title.Trim().Length is 0 or > ShiftScheduleCode.MaxTitleLength
            || entry.Description.Trim().Length > ShiftScheduleCode.MaxDescriptionLength
            || entry.TimeSeconds < 0 || entry.TimeSeconds > ShiftScheduleCode.MaxTimeSeconds)
            {
                return false;
            }
        }

        var elapsed = _ticker.RunLevel == GameRunLevel.InRound ? _ticker.RoundDuration() : TimeSpan.Zero;
        var entries = sourse
            .Select(entry => new ShiftScheduleEntry(
                schedule.NextId++,
                entry.Title.Trim(),
                entry.TimeSeconds,
                entry.Description.Trim())
            {
                Announced = _ticker.RunLevel == GameRunLevel.InRound &&
                    TimeSpan.FromSeconds(entry.TimeSeconds) <= elapsed
            })
            .OrderBy(entry => entry.TimeSeconds)
            .ThenBy(entry => entry.Id)
            .ToArray();

        schedule.Entries.Clear();
        schedule.Entries.AddRange(entries);
        schedule.Enabled = enabled;
        return true;
    }

    [SubscribeLocalEvent]
    private void OnRemove(EntityUid uid, ShiftScheduleConsoleComponent component, ShiftScheduleRemoveMessage message)
    {
        if (message.Actor is not { Valid: true } user)
            return;

        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        if (TryComp<AccessReaderComponent>(uid, out var accessReader)
        && !_accessReader.IsAllowed(user, uid, accessReader))
        {
            UpdateInterface(uid, schedule, false, Loc.GetString("shift-schedule-no-access"));
            return;
        }

        if (schedule.Entries.RemoveAll(entry => entry.Id == message.Id) == 0)
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-entry-missing"));
            return;
        }

        UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-removed"));
    }

    [SubscribeLocalEvent]
    private void OnToggle(EntityUid uid, ShiftScheduleConsoleComponent component, ShiftScheduleToggleMessage message)
    {
        if (message.Actor is not { Valid: true } user)
            return;

        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        if (TryComp<AccessReaderComponent>(uid, out var accessReader) &&
            !_accessReader.IsAllowed(user, uid, accessReader))
        {
            UpdateInterface(uid, schedule, false, Loc.GetString("shift-schedule-no-access"));
            return;
        }

        schedule.Enabled = message.Enabled;
        UpdateInterface(uid, schedule, true, Loc.GetString(schedule.Enabled
            ? "shift-schedule-enabled"
            : "shift-schedule-disabled"));
    }

    [SubscribeLocalEvent]
    private void OnAnnounce(EntityUid uid, ShiftScheduleConsoleComponent component, ShiftScheduleAnnounceMessage message)
    {
        if (message.Actor is not { Valid: true } user)
            return;

        if (!TryGetSchedule(uid, out var schedule))
        {
            _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
                new ShiftScheduleConsoleInterfaceState
                (
                    false,
                    false,
                    Array.Empty<ShiftScheduleEntryState>(),
                    Loc.GetString("shift-schedule-station-missing")
                )
            );

            return;
        }

        if (TryComp<AccessReaderComponent>(uid, out var accessReader) &&
            !_accessReader.IsAllowed(user, uid, accessReader))
        {
            UpdateInterface(uid, schedule, false, Loc.GetString("shift-schedule-no-access"));
            return;
        }

        if (!schedule.Enabled)
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-disabled"));
            return;
        }

        var entry = schedule.Entries.FirstOrDefault(candidate => candidate.Id == message.Id);
        if (entry == null)
        {
            UpdateInterface(uid, schedule, true, Loc.GetString("shift-schedule-entry-missing"));
            return;
        }

        Announce(schedule, entry, advance: true);
        UpdateInterface(uid, schedule, true,
            Loc.GetString("shift-schedule-announced-in-advance", ("name", entry.Title)));
    }

    [SubscribeLocalEvent]
    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.InRound)
            return;

        var query = EntityQueryEnumerator<ShiftScheduleComponent>();
        while (query.MoveNext(out _, out var schedule))
        {
            foreach (var entry in schedule.Entries)
            {
                entry.Announced = false;
            }
        }
    }

    private bool TryGetSchedule(EntityUid console, out ShiftScheduleComponent schedule)
    {
        if (_stationSystem.GetOwningStation(console) is { } station
        && TryComp(station, out ShiftScheduleComponent? stationSchedule))
        {
            schedule = stationSchedule;
            return true;
        }

        schedule = null!;
        return false;
    }

    private void UpdateInterface(EntityUid uid, ShiftScheduleComponent schedule, bool canEdit, string? status = null, bool clear = false)
    {
        var entries = canEdit
            ? schedule.Entries
                .Select(entry => new ShiftScheduleEntryState(entry.Id, entry.Title, entry.TimeSeconds, entry.Description))
                .ToArray()
            : Array.Empty<ShiftScheduleEntryState>();

        _ui.SetUiState(uid, ShiftScheduleConsoleUiKey.Key,
            new ShiftScheduleConsoleInterfaceState(canEdit, schedule.Enabled, entries, status, clear));
    }

    private void Announce(ShiftScheduleComponent schedule, ShiftScheduleEntry entry, bool advance = false)
    {
        var message = advance
            ? string.IsNullOrWhiteSpace(entry.Description)
                ? Loc.GetString("shift-schedule-advance-no-description", ("name", entry.Title))
                : Loc.GetString("shift-schedule-advance-announcement", ("name", entry.Title), ("description", entry.Description))
            : string.IsNullOrWhiteSpace(entry.Description)
                ? Loc.GetString("shift-schedule-announcement-no-description", ("name", entry.Title))
                : Loc.GetString("shift-schedule-announcement", ("name", entry.Title), ("description", entry.Description));

        var maxLength = _configuration.GetCVar(CCVars.ChatMaxAnnouncementLength);
        message = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
        schedule.Entries.Remove(entry);
        _chat.DispatchGlobalAnnouncement(message, Loc.GetString("shift-schedule-announcement-title"));
    }
}
