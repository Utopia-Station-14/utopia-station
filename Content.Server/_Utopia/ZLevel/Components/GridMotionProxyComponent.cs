using Robust.Shared.GameObjects;

namespace Content.Server._Utopia.ZLevel.Components;

[RegisterComponent]
public sealed partial class GridMotionProxyComponent : Component
{
    public EntityUid Grid;
    public EntityUid SyncGroup;
    public bool IsMaster;
}