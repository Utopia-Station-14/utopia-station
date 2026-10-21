using Robust.Shared.Maths;

namespace Content.Shared._Utopia.ModSuits.Modules.Flashlight;

[RegisterComponent]
public sealed partial class ModSuitFlashlightModuleComponent : Component
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public float Energy = 2f;

    [DataField]
    public Color Color = Color.FromHex("#FFEEDD");
}
