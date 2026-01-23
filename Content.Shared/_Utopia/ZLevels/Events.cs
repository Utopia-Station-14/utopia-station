using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Shared._Utopia.ZLevels.Events;

public sealed class GridMotionSyncEvent : EntityEventArgs
{
    public Vector2 LinearVelocity;
    public float AngularVelocity;
    public Angle Rotation;
}
