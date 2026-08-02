using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.AddComponentsOnUse;

[RegisterComponent]
public sealed partial class AddComponentsOnUseComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public bool DeleteOnUse = true;
}
