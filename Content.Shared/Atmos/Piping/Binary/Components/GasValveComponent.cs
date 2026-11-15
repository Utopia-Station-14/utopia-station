using Content.Shared.Atmos.Piping.Binary.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Binary.Components;

/// <summary>
/// Component for manual atmospherics pumps that can open or close to let gas through.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedGasValveSystem))]
public sealed partial class GasValveComponent : Component
{
    /// <summary>
    /// Whether the valve is currently open and letting gas through.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public bool Open = true;

    /// <summary>
    /// Inlet for the nodecontainer.
    /// </summary>
    [DataField("inlet")]
    public string InletName = "inlet";

    /// <summary>
    /// Outlet for the nodecontainer.
    /// </summary>
    [DataField("outlet")]
    public string OutletName = "outlet";

    /// <summary>
    /// Sound when <see cref="Open"/> is toggled.
    /// </summary>
    [DataField]
    public SoundSpecifier ValveSound = new SoundCollectionSpecifier("valveSqueak");

    // Utopia-Tweak : AtmosUpdt
    [DataField]
    public GasValveType Kind = GasValveType.Straight;

    [DataField]
    public string? SideName;

    [DataField]
    public string? InletBName;

    [DataField]
    public string? OutletBName;
    // Utopia-Tweak : AtmosUpdt
}

// Utopia-Tweak : AtmosUpdt
[Serializable, NetSerializable]
public enum GasValveType : byte { Straight, ThreeWay, FourWay }
// Utopia-Tweak : AtmosUpdt
