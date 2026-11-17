using Content.Shared.Construction.Prototypes;
using Content.Shared.DeviceLinking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Cargo.Components;

/// <summary>
/// Handles teleporting in requested cargo after the specified delay.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedCargoSystem)), AutoGenerateComponentPause]
public sealed partial class CargoTelepadComponent : Component
{
    [DataField]
    public List<CargoOrderData> CurrentOrders = new();

    /// <summary>
    /// The delay between each teleport in seconds
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The next time the teleporter can teleport.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextTeleport;

    [DataField]
    public CargoTelepadState CurrentState = CargoTelepadState.Unpowered;

    [DataField]
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Machines/phasein.ogg");

    /// <summary>
    ///     The paper-type prototype to spawn with the order information.
    /// </summary>
    [DataField]
    public EntProtoId PrinterOutput = "PaperCargoInvoice";

    [DataField]
    public ProtoId<SinkPortPrototype> ReceiverPort = "OrderReceiver";

    // Utopia-Tweak : Machine Parts
    /// <summary>
    /// The base amount of time it takes to teleport from the telepad
    /// </summary>
    [DataField]
    public TimeSpan BaseDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The machine part that affects <see cref="Delay"/>
    /// </summary>
    [DataField]
    public ProtoId<MachinePartPrototype> MachinePartTeleportDelay = "Capacitor";

    /// <summary>
    /// A multiplier applied to <see cref="Delay"/> for each level of <see cref="MachinePartTeleportDelay"/>
    /// </summary>
    [DataField]
    public float PartTierTeleportDelay = 0.8f;
    // Utopia-Tweak : Machine Parts
}
