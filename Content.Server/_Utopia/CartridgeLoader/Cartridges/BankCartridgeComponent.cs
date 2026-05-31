namespace Content.Server.CartridgeLoader;

[RegisterComponent]
public sealed partial class BankCartridgeComponent : Component
{
    [ViewVariables]
    public int? AccountId;

    [ViewVariables]
    public EntityUid? Loader;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool NotificationOn = true;

    public string AccountLinkResult = string.Empty;

    public string TransferResult = string.Empty;
}
