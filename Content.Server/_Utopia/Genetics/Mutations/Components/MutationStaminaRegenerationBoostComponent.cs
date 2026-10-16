namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationStaminaRegenerationBoostComponent : Component
{
    [DataField]
    public float RegenBonus = 1.5f;
}
