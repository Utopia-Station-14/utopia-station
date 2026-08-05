namespace Content.Server.Atmos.Components
{
    [RegisterComponent]
    public sealed partial class GasTankMixerComponent : Component
    {
        public const string SlotAName = "gas_tank_a";
        public const string SlotBName = "gas_tank_b";

        [ViewVariables(VVAccess.ReadWrite)]
        public float Timer = 10f;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool IsActive = false;
    }
}