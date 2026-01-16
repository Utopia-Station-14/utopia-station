using Content.Shared.Buckle.Components;
using Robust.Shared.GameStates;
using Content.Shared.Construction.Prototypes; // Frontier
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype; // Frontier

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


    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
    public string MachinePartMetabolismModifier = "Capacitor";
    // Utopia-Tweak : Machine Parts
}
