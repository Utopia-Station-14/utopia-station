using Content.Shared.Whitelist;

namespace Content.Shared._Utopia.CantShoot;

[RegisterComponent]
public sealed partial class CantShootComponent : Component
{
    [DataField]
    public string? Popup;

    [DataField]
    public EntityWhitelist? Whitelist;
}
