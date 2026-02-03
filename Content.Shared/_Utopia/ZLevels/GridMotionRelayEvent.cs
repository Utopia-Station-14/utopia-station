using System.Numerics;

namespace Content.Shared._Utopia.ZLevels.Events;

public sealed class GridMotionRelayEvent : EntityEventArgs
{
    public EntityUid SourceGrid { get; }
    public Vector2 LinearVelocity { get; }
    public float AngularVelocity { get; }

    public GridMotionRelayEvent(EntityUid sourceGrid, Vector2 linearVelocity, float angularVelocity)
    {
        SourceGrid = sourceGrid;
        LinearVelocity = linearVelocity;
        AngularVelocity = angularVelocity;
    }
}
