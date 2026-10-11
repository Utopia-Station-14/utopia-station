/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class ZLevelAtmosSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    private const int GasCount = Atmospherics.TotalNumberOfGases;
    private const float MinCapacity = 1e-6f;

    #region Constants

    [ViewVariables] public float DensityStrength = 1f;
    [ViewVariables] public float ThermalStrength = 0.5f;
    [ViewVariables] public float MaxSegregation = 2f;
    [ViewVariables] public float FlowRate = 1f;
    [ViewVariables] public float ConductionRate = 1f;
    [ViewVariables] public float MinMovedMoles => 0.01f;
    [ViewVariables] public float MinTemperatureDelta = 0.5f;

    #endregion

    #region Gas Tables

    private readonly float[] _density = new float[GasCount];
    private readonly float[] _heatCapacity = new float[GasCount];
    private readonly float[] _heatWeight = new float[GasCount];
    private readonly float[] _lift = new float[GasCount];
    private readonly float[] _ventFactor = new float[GasCount];

    private float _referenceDensity;

    public float GetMolarHeatCapacity(int gas) => _heatCapacity[gas];
    public float GetLift(int gas) => _lift[gas];
    public float GetVentFactor(int gas) => _ventFactor[gas];

    #endregion

    public override void Initialize()
    {
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        RebuildTables();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<GasPrototype>())
            RebuildTables();
    }

    private void RebuildTables()
    {
        Array.Clear(_density);
        Array.Clear(_heatCapacity);

        foreach (var gas in Enum.GetValues<Gas>())
        {
            var i = (int)gas;
            if (i < 0 || i >= GasCount || !_proto.TryIndex<GasPrototype>(gas.ToString(), out var proto))
                continue;

            _density[i] = MathF.Max(0f, proto.Density);
            _heatCapacity[i] = MathF.Max(0f, proto.MolarHeatCapacity);
        }

        _referenceDensity = AirAverage(_density);
        var referenceCapacity = AirAverage(_heatCapacity);

        for (var i = 0; i < GasCount; i++)
        {
            if (_density[i] <= 0f)
                _density[i] = _referenceDensity;

            _lift[i] = _referenceDensity > 0f ? Math.Clamp((_referenceDensity - _density[i]) / _referenceDensity, -1f, 1f) : 0f;
            _ventFactor[i] = Math.Clamp(1f + _lift[i], 0.25f, 2f);
            _heatWeight[i] = _heatCapacity[i] > 0f && referenceCapacity > 0f ? Math.Clamp(_heatCapacity[i] / referenceCapacity, 0.25f, 4f) : 1f;
        }
    }

    private static float AirAverage(float[] values)
    {
        var o2 = values[(int)Gas.Oxygen];
        var n2 = values[(int)Gas.Nitrogen];
        if (o2 > 0f && n2 > 0f)
            return 0.21f * o2 + 0.79f * n2;

        var sum = 0f;
        var count = 0;
        foreach (var value in values)
        {
            if (value <= 0f) continue;
            sum += value;
            count++;
        }
        return count > 0 ? sum / count : 0f;
    }

    public float FlowFraction(int neighbours)
        => MathF.Min(1f, 2f * FlowRate / (neighbours + 1));

    public float ProcessFlows(float[] lower, float tLower, float volLower, float[] upper, float tUpper, float volUpper,
        int neighbours, bool allowDown, float[] flows)
    {
        var totalLower = 0f;
        var totalUpper = 0f;
        var weighted = 0f;

        for (var i = 0; i < GasCount; i++)
        {
            var l = lower[i];
            var u = upper[i];
            totalLower += l;
            totalUpper += u;
            weighted += (l + u) * _density[i];
        }

        var total = totalLower + totalUpper;
        if (total <= Atmospherics.GasMinMoles)
        {
            Array.Clear(flows, 0, GasCount);
            return 0f;
        }

        var meanDensity = weighted / total;
        var rate = FlowFraction(neighbours);
        var maxExponent = MathF.Log(MathF.Max(1f, MaxSegregation));
        var thermal = ThermalStrength > 0f && tLower > 0f && tUpper > 0f
            ? Math.Clamp(ThermalStrength * MathF.Log(tLower / tUpper), -maxExponent, maxExponent)
            : 0f;

        var volumeRatio = volLower > 0f && volUpper > 0f ? volUpper / volLower : 1f;
        var moved = 0f;

        for (var i = 0; i < GasCount; i++)
        {
            var l = lower[i];
            var u = upper[i];
            if (l + u <= Atmospherics.GasMinMoles)
            {
                flows[i] = 0f;
                continue;
            }

            var exponent = thermal * _heatWeight[i];
            if (meanDensity > 0f)
                exponent += DensityStrength * Math.Clamp((meanDensity - _density[i]) / meanDensity, -1f, 1f);

            exponent = Math.Clamp(exponent, -maxExponent, maxExponent);
            var k = (MathF.Abs(exponent) < 1e-4f ? 1f : MathF.Exp(exponent)) * volumeRatio;
            var x = (k * l - u) / (1f + k) * rate;

            if (x < 0f && !allowDown)
                x = 0f;

            flows[i] = x;
            moved += MathF.Abs(x);
        }

        var minMoved = MathF.Max(0.01f, total * 0.0005f * (1f / MathF.Max(1f, volumeRatio)));
        return moved >= minMoved ? moved : 0f;
    }

    public void MixTemperatures(float capLower, float tLower, float capUpper, float tUpper, float capUp, float capDown,
        int neighbours, bool allowDown, out float newLower, out float newUpper)
    {
        var cLower = capLower - capUp + capDown;
        var cUpper = capUpper + capUp - capDown;
        var eLower = capLower * tLower - capUp * tLower + capDown * tUpper;
        var eUpper = capUpper * tUpper + capUp * tLower - capDown * tUpper;

        newLower = cLower > MinCapacity ? eLower / cLower : tLower;
        newUpper = cUpper > MinCapacity ? eUpper / cUpper : tUpper;

        if (ConductionRate <= 0f || cLower <= MinCapacity || cUpper <= MinCapacity)
            return;

        var delta = newLower - newUpper;
        if (MathF.Abs(delta) < MinTemperatureDelta || (!allowDown && delta <= 0f))
            return;

        var heat = delta * (cLower * cUpper / (cLower + cUpper)) * FlowFraction(neighbours) * ConductionRate;
        newLower -= heat / cLower;
        newUpper += heat / cUpper;
    }

    public float Vent(float[] moles, float ratio)
    {
        var removed = 0f;
        for (var i = 0; i < GasCount; i++)
        {
            var amount = moles[i];
            if (amount <= 0f) continue;

            var take = amount * MathF.Min(1f, ratio * _ventFactor[i]);
            moles[i] = amount - take;
            removed += take;
        }
        return removed;
    }
}
