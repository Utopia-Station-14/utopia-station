using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Clothing
{
    [RegisterComponent]
    public sealed partial class ClothingGrantComponentComponent : Component
    {
        [DataField("component", required: true)]
        [AlwaysPushInheritance]
        public ComponentRegistry Components { get; private set; } = new();

        [ViewVariables]
        public bool IsActive = false;
    }
}
