using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Atmos.EntitySystems;

namespace Content.Client._Utopia.Atmos
{
    public sealed partial class GasVisualsSystem : EntitySystem
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        private readonly Dictionary<string, GasPrototype> _gasColors = new();

        public override void Initialize()
        {
            base.Initialize();

            CachePrototypes();
            _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _prototypeManager.PrototypesReloaded -= OnPrototypesReloaded;
        }

        private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
        {
            if (args.WasModified<GasPrototype>())
                CachePrototypes();
        }

        private void CachePrototypes()
        {
            _gasColors.Clear();
            foreach (var proto in _prototypeManager.EnumeratePrototypes<GasPrototype>())
            {
                _gasColors[proto.ID] = proto;
            }
        }

        public Color GetGasColor(string gasId, ThermalByte thermalByte)
        {
            if (!_gasColors.TryGetValue(gasId, out var gasColorProto) ||
                gasColorProto.ColorGradient == null ||
                gasColorProto.ColorGradient.Count == 0)
                return Color.White;

            if (thermalByte.IsAtmosImpossible || thermalByte.IsVacuum)
                return Color.White;

            if (!thermalByte.TryGetTemperature(out float temperature, onVacuumReturnTcmb: false))
                return Color.White;

            if (temperature <= gasColorProto.GradientMinTemp)
                return gasColorProto.ColorGradient[0];

            if (temperature >= gasColorProto.GradientMaxTemp)
                return gasColorProto.ColorGradient[^1];

            var t = (temperature - gasColorProto.GradientMinTemp) /
                    (gasColorProto.GradientMaxTemp - gasColorProto.GradientMinTemp);

            var colorCount = gasColorProto.ColorGradient.Count;
            var scaledT = t * (colorCount - 1);
            var index = (int)scaledT;
            float fraction = scaledT - index;

            if (index >= colorCount - 1)
                return gasColorProto.ColorGradient[^1];

            return Color.InterpolateBetween(gasColorProto.ColorGradient[index],
                gasColorProto.ColorGradient[index + 1],
                fraction);
        }
    }
}
