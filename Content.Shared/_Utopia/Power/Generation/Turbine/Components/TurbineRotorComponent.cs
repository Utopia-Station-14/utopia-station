using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Power.Turbines.Components
{
    [RegisterComponent]
    public sealed partial class TurbineRotorComponent : Component
    {
        /// <summary>
        /// Включатель турбины.
        /// </summary>
        [DataField]
        public bool IsActive = true;

        /// <summary>
        /// Модификатор для выработки электропитания
        /// </summary>
        [DataField] 
        public float Efficiency = 0.7f;

        /// <summary>
        /// Минимальное давление, после которого ротор начинает вырабатывать электроенергию.
        /// </summary>
        [DataField] 
        public float MinPressure = 500f;

        [DataField] 
        public float CurrentPressure;
        
        [DataField] 
        public float MaxPressure = 16101f;
        /// <summary>
        /// Модификатор оборотов в минуту. Чем меньше значение - тем больше оборотов в минуту будет делать турбина.
        /// </summary>
        [DataField] 
        public float RpmFactor = 1.5f;

        [DataField] 
        public float CurrentTemperature;

        /// <summary>
        /// Максимальное количество оборотов, после которого ротор начнет получать урон.
        /// </summary>
        [DataField] 
        public float MaxRPM = 4000f;

        /// <summary>
        /// Максимальная температура газа, после которой ротор начнет получать урон.
        /// </summary>
        [DataField] 
        public float MaxTemperature = 1200f;

        /// <summary>
        /// Вывод оборотов в минуту.
        /// </summary>
        [ViewVariables(VVAccess.ReadOnly)]
        public float CurrentRPM;

        /// <summary>
        /// ХП турбины.
        /// </summary>
        [ViewVariables(VVAccess.ReadOnly)]
        public float Integrity = 100f;

        [DataField] 
        public float Energy;

        [DataField] 
        public float TemperatureDamage;
        [DataField] 
        public float PressureDamage;
        [DataField] 
        public float EnergyDamage;
        [DataField] 
        public float DamageArchived;

        [DataField]
        public float TalkingTimer = 60;
    }

    public enum ExplosionType
    {
        Heat,
        EMP,
        BIGSHOT
    }
}