using Robust.Shared.GameObjects;

namespace Content.Shared.Power.Turbines.Components
{
    [RegisterComponent]
    public sealed partial class TurbineOutletComponent : Component
    {
        /// <summary>
        /// Модификатор для количества выходного газа.
        /// <seealso cref = "TurbineSystem">
        /// </summary>
        [DataField]
        public float GasOutletModificator = 2;
    }
}