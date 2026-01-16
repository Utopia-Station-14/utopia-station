namespace Content.Server.Chemistry.Components;

[RegisterComponent]
public sealed partial class SolutionHeaterComponent : Component
{
    /// <summary>
    /// How much heat is added per second to the solution, taking upgrades into account.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float HeatPerSecond;

    // Utopia-Tweak : Machine Parts
    [ViewVariables(VVAccess.ReadWrite)]
    public float HeatMultiplier = 1;

    [DataField]
    public string MachinePartHeatPerSecond = "Capacitor";

    [DataField]
    public float PartTierHeatMultiplier = 1.5f;
    // Utopia-Tweak : Machine Parts
}
