namespace Content.Server._Utopia.Supermatter.Components;

[RegisterComponent]
public sealed partial class SupermatterFoodComponent : Component
{
    [DataField]
    public float Energy { get; set; }
}
