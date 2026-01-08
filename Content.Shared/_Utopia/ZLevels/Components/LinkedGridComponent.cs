using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.ZLevels.Components;

[RegisterComponent]
public sealed partial class ZLinkedGridComponent : Component
{
    [DataField(required: true)]
    public string LinkGroupId = default!;

    [DataField]
    public int ZLevel;

    [DataField(required: true)]
    public Vector2i TileOffset = Vector2i.Zero;

    [DataField]
    public bool IsAnchor;

    [ViewVariables]
    public readonly HashSet<EntityUid> LinkedGrids = new();
}