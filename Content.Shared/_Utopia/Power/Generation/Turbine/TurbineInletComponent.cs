using Robust.Shared.GameObjects;

namespace Content.Shared.Power.Turbines.Components
{
    [RegisterComponent]
    public sealed partial class TurbineInletComponent : Component
    {
        /// <summary>
        /// Количество газа, которое компрессор собирает за 1 раз.
        /// </summary>
        [DataField]
        public float GasIntake = 0;
    }
}