using Content.Shared.Buckle.Components;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared.Bed.Components;

/// <summary>
/// A <see cref="StrapComponent"/> that modifies a strapped entity's metabolic rate by the given multiplier
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BedSystem))]
public sealed partial class StasisBedComponent : Component
{
    /// <summary>
    /// What the metabolic update rate will be multiplied by (higher = slower metabolism)
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Multiplier = 10f;

    // Utopia-Tweak : Machine Parts
    [DataField]
    public float BaseMultiplier = 10f;


    [DataField]
    public ProtoId<MachinePartPrototype> MachinePartMetabolismModifier = "Capacitor";
    // Utopia-Tweak : Machine Parts
}
