namespace Content.Server._Utopia.ModSuits.Systems;

[RegisterComponent]
public sealed partial class ModSuitModuleComponent : Component
{
    [DataField]
    public int Complexity;

    [DataField]
    public float Expenditure = 0.2f;

    [DataField]
    public bool Active = true;
}
