using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Utopia.ZLevels.Components;

[RegisterComponent]
public sealed partial class GridMotionLinkComponent : Component
{
    [DataField]
    public bool IsMaster;

    [ViewVariables]
    public EntityUid? MasterGrid;
}
