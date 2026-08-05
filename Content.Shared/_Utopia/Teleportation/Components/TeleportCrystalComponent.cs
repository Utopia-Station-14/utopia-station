namespace Content.Shared._Utopia.Teleportation;


[RegisterComponent]
public sealed partial class TeleportCrystalComponent : Component
{
    [DataField]
    public bool MobOnly = true;

    [DataField]
    public float SpecialValue = 20f;

    [DataField]
    public bool ConsumeOnThrow = true;

    [DataField]
    public CrystalType CType = CrystalType.Bluespace;

    [DataField]
    public float Cooldown = 15f;
}

[Serializable]
public enum CrystalType : byte
{
    Bluespace,
    Redspace,
    Purplespace,
}

[RegisterComponent]
public sealed partial class RedspaceEffectComponent : Component;
