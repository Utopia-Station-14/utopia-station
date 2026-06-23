namespace Content.Server.Teleportation;

[RegisterComponent]
public sealed partial class RandomTeleportOnUseComponent : Component
{
    [DataField]
    public bool ConsumeOnUse = true;
}
