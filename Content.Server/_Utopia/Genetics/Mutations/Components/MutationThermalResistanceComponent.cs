namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationThermalResistanceComponent : Component
{
    [DataField]
    public float HeatingCoefficient = 1.0f;

    [DataField]
    public float CoolingCoefficient = 1.0f;
}
