namespace Content.Server._Utopia.Bluespace;


[RegisterComponent]
public sealed partial class BluespaceCrystalComponent : Component
{
    [DataField]
    public bool MobOnly = true;

    [DataField]
    public float TeleportRadiusThrow = 20f;

    [DataField]
    public bool ConsumeOnThrow = true;
}
