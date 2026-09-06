namespace Content.Server._Utopia.Teleportation;

[RegisterComponent]
public sealed partial class RandomTeleportOnUseComponent : Component
{
    [DataField]
    public bool ConsumeOnUse = true;
}
