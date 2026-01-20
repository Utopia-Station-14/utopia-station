using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server._Utopia.GridSync
{
    [RegisterComponent]
    public sealed partial class GridSyncGroupComponent : Component
    {
        [DataField(required: true)]
        public string GroupId = default!;

        [DataField]
        public float Weight = 1f;

        [DataField]
        public float LerpStrength = 5f;
    }
}