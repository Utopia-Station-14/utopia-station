using Content.Shared.Atmos;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Guidebook;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Atmos.Piping.Unary.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
    public sealed partial class GasThermoMachineComponent : Component
    {
        [DataField("inlet")]
        public string InletName = "pipe";

        /// <summary>
        ///     Current electrical power consumption, in watts. Increasing power increases the ability of the
        ///     thermomachine to heat or cool air.
        /// </summary>
        [DataField]
        [GuidebookData]
        public float HeatCapacity = 5000;

        [DataField, AutoNetworkedField]
        public float TargetTemperature = Atmospherics.T20C;

        /// <summary>
        ///     Tolerance for temperature setpoint hysteresis.
        /// </summary>
        [GuidebookData]
        [DataField, ViewVariables(VVAccess.ReadOnly)]
        public float TemperatureTolerance = 2f;

        /// <summary>
        ///     Implements setpoint hysteresis to prevent heater from rapidly cycling on and off at setpoint.
        ///     If true, add Sign(Cp)*TemperatureTolerance to the temperature setpoint.
        /// </summary>
        [ViewVariables(VVAccess.ReadOnly)]
        public bool HysteresisState;

        /// <summary>
        ///     Coefficient of performance. Output power / input power.
        ///     Positive for heaters, negative for freezers.
        /// </summary>
        [DataField("coefficientOfPerformance")]
        public float Cp = 0.9f; // output power / input power, positive is heat

        /// <summary>
        ///     Current minimum temperature
        ///     Ignored if heater.
        /// </summary>
        [DataField, AutoNetworkedField]
		[GuidebookData]
        public float MinTemperature = 73.15f;

        /// <summary>
        ///     Current maximum temperature
        ///     Ignored if freezer.
        /// </summary>
        [DataField, AutoNetworkedField]
		[GuidebookData]
        public float MaxTemperature = 593.15f;

        /// <summary>
        /// Last amount of energy added/removed from the attached pipe network
        /// </summary>
        [DataField]
        public float LastEnergyDelta;

        /// <summary>
        /// An percentage of the energy change that is leaked into the surrounding environment rather than the inlet pipe.
        /// </summary>
        [DataField]
		[GuidebookData]
       	public float EnergyLeakPercentage;

        /// <summary>
        /// If true, heat is exclusively exchanged with the local atmosphere instead of the inlet pipe air
        /// </summary>
        [DataField]
        public bool Atmospheric;

        // Utopia-Tweak : Machine Parts

        [DataField]
        public float BaseHeatCapacity = 5000;

        /// <summary>
        ///     Minimum temperature the device can reach with a 0 total laser quality. Usually the quality will be at
        ///     least 1.
        /// </summary>
        [DataField]
        public float BaseMinTemperature = 96.625f; // Selected so that tier-1 parts can reach 73.15k

        /// <summary>
        ///     Maximum temperature the device can reach with a 0 total laser quality. Usually the quality will be at
        ///     least 1.
        /// </summary>
        [DataField]
        public float BaseMaxTemperature = Atmospherics.T20C;

        /// <summary>
        ///     Decrease in minimum temperature, per unit machine part quality.
        /// </summary>
        [DataField]
        public float MinTemperatureDelta = 23.475f; // selected so that tier-4 parts can reach TCMB

        /// <summary>
        ///     Change in maximum temperature, per unit machine part quality.
        /// </summary>
        [DataField]
        public float MaxTemperatureDelta = 300;

        /// <summary>
        ///     The machine part that affects the heat capacity.
        /// </summary>
        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
        public string MachinePartHeatCapacity = "MatterBin";

        /// <summary>
        ///     The machine part that affects the temperature range.
        /// </summary>
        [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<MachinePartPrototype>))]
        public string MachinePartTemperature = "Laser";
        // Utopia-Tweak : Machine Parts
    }
}
