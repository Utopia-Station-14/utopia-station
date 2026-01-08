using Robust.Shared.GameObjects;

namespace Content.Shared.ZLevels;

public sealed class ZLinkedGridEvent : EntityEventArgs
{
    public EntityUid SourceGrid;
    public string EventId = default!;
    public object? Payload;
}