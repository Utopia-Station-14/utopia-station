namespace Content.Shared.ADT.Clothing;

[RegisterComponent]
public sealed partial class ClothingGrantTagComponent : Component
{
    [DataField(required: true)]
    public string Tag;

    [ViewVariables]
    public bool IsActive = false;
}
