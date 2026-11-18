namespace Content.Server.Kitchen.Components;

[RegisterComponent]
public sealed partial class MicrowaveBatteryComponent : Component
{
    [DataField]
    public float CookingPowerDraw = 90f;

    [DataField]
    public bool NetworkPower = true;

    [DataField]
    public LocId BatterySwitch = "microwave-switch";
}
