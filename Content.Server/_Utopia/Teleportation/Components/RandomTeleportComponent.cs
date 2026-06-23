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
    public SoundSpecifier TeleportSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
}
