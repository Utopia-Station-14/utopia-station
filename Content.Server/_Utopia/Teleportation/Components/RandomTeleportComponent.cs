using Robust.Shared.Audio;

namespace Content.Server.Teleportation;

[RegisterComponent]
public sealed partial class RandomTeleportComponent : Component
{
    [DataField]
    public float TeleportRadius = 100f;

    [DataField]
    public int TeleportAttempts = 20;

    [DataField]
    public SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    [DataField]
    public SoundSpecifier DepartureSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}
