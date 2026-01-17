using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using System.Numerics;

namespace Content.Shared._Utopia.ZLevels.Components;

[RegisterComponent]
public sealed partial class GridMotionMonitoringComponent : Component
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 Direction = Vector2.Zero;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float Speed;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float AngularSpeed;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public Angle CurrentAngle = Angle.Zero;
}