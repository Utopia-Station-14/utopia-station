using Robust.Shared.GameObjects;
 using Robust.Shared.Serialization;

namespace Content.Shared.Power.Turbines.Components
{
    [RegisterComponent]
    public sealed partial class TurbineOutletComponent : Component
    {
        /// <summary>
        /// Модификатор для количества выходного газа.
        /// </summary>
        [DataField]
        public float GasOutletModificator = 2;
    }
    
    [Serializable, NetSerializable]
    public enum TurbineVisuals : byte
    {
        Spinning
    }
}