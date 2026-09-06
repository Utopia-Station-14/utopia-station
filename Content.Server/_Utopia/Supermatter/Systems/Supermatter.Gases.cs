using System.Numerics;
using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Atmos;
using Content.Shared._Utopia.Supermatter.Prototypes;
using Content.Shared._Utopia.Supermatter.Events;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    private const float WasteGasHeatingConstant = 1.5f;

    private GasMixture CollectGases(Entity<SupermatterComponent> sm)
    {
        var result = new GasMixture();
        var xform = Transform(sm);

        if (xform.GridUid is not { } gridUid)
            return result;

        var centerTile = _transform.GetGridOrMapTilePosition(sm, xform);
        var centerMixture = _atmosphere.GetTileMixture(gridUid, null, centerTile, true);

        if (centerMixture != null)
            sm.Comp.CurrentTemperature = centerMixture.Temperature;

        foreach (var (offset, ratio) in TileCollectionRatios)
        {
            var tileMixture = _atmosphere.GetTileMixture(gridUid, null, centerTile + offset, true);
            if (tileMixture == null)
                continue;

            for (var i = 0; i < Atmospherics.AdjustedNumberOfGases; i++)
            {
                var moles = tileMixture.GetMoles(i);
                if (moles > Atmospherics.GasMinMoles)
                    result.AdjustMoles(i, moles * ratio);
            }
        }

        return result;
    }

    public void ThrowUp(Entity<SupermatterComponent> sm)
    {
        var power = sm.Comp.ExternalEnergy - sm.Comp.InternalEnergy;
        var wastes = ModifyWasteGas(sm, sm.Comp.WasteGas, power);
        var xform = Transform(sm);

        if (xform.GridUid is not { } gridUid)
            return;

        var centerTile = _transform.GetGridOrMapTilePosition(sm, xform);
        var tileMixture = _atmosphere.GetTileMixture(gridUid, null, centerTile, true);

        if (tileMixture != null)
        {
            _atmosphere.Merge(tileMixture, wastes);
            if (power > 0)
            {
                var heatCapacity = _atmosphere.GetHeatCapacity(tileMixture, true);
                var temp = power / heatCapacity;

                if (heatCapacity > 0)
                {
                    tileMixture.Temperature += MathF.Max(tileMixture.Temperature + temp, Atmospherics.TCMB);
                    ChangeInternalEnergy(sm, -temp);
                }
            }
        }
    }

    public void AddWasteGas(Entity<SupermatterComponent> sm, Gas gas, float amount)
    {
        if (gas == Gas.Oxygen || gas == Gas.Plasma || amount <= 0)
            return;

        var currentOxygen = sm.Comp.WasteGas.GetMoles(Gas.Oxygen);
        var availableSpace = MathF.Max(0f, currentOxygen - MinOxygenWaste);
        var amountToAdd = MathF.Min(amount, availableSpace);

        if (amountToAdd <= 0)
            return;

        var currentTargetGas = sm.Comp.WasteGas.GetMoles(gas);

        sm.Comp.WasteGas.SetMoles(gas, currentTargetGas + amountToAdd);
        sm.Comp.WasteGas.SetMoles(Gas.Oxygen, currentOxygen - amountToAdd);
    }

    public void RemoveWasteGas(Entity<SupermatterComponent> sm, Gas gas, float amount)
    {
        if (gas == Gas.Oxygen || gas == Gas.Plasma || amount <= 0)
            return;

        var currentTargetGas = sm.Comp.WasteGas.GetMoles(gas);
        var amountToRemove = MathF.Min(amount, currentTargetGas);

        if (amountToRemove <= 0)
            return;

        var currentOxygen = sm.Comp.WasteGas.GetMoles(Gas.Oxygen);

        sm.Comp.WasteGas.SetMoles(gas, currentTargetGas - amountToRemove);
        sm.Comp.WasteGas.SetMoles(Gas.Oxygen, MathF.Min(MaxOxygenWaste, currentOxygen + amountToRemove));
    }

    private GasMixture ModifyWasteGas(Entity<SupermatterComponent> sm, GasMixture wastes, float power)
    {
        var heatModifier = WasteGasHeatingConstant * sm.Comp.TemperatureScaleModificator;
        if (power > 0)
        {
            var plasmaGen = MathF.Max(power * heatModifier * 1f, 0f); // sm.Comp.PlasmaReleaseModifier
            var oxygenGen = MathF.Max((power + wastes.Temperature * heatModifier - Atmospherics.T0C) * 1f, 0f); //sm.Comp.OxygenReleaseEfficiencyModifier

            wastes.SetMoles(Gas.Plasma, wastes.GetMoles(Gas.Plasma) + plasmaGen);
            wastes.SetMoles(Gas.Oxygen, wastes.GetMoles(Gas.Oxygen) + oxygenGen);
        }

        wastes.Temperature = MathF.Max(wastes.Temperature + heatModifier, Atmospherics.TCMB);
        return wastes;
    }

    public void ProcessGases(Entity<SupermatterComponent> sm, float frameTime)
    {
        sm.Comp.AtmosGas = CollectGases(sm);
        var totalMoles = sm.Comp.AtmosGas.TotalMoles;

        if (totalMoles <= Atmospherics.GasMinMoles)
        {
            DecayModificators(sm.Comp, frameTime);
            return;
        }

        Span<float> ratios = stackalloc float[Atmospherics.AdjustedNumberOfGases];
        var knownGasRatioSum = 0f;
        var targetMods = Vector4.Zero;

        for (var i = 0; i < Atmospherics.AdjustedNumberOfGases; i++)
        {
            var moles = sm.Comp.AtmosGas.GetMoles(i);
            if (moles <= Atmospherics.GasMinMoles) continue;

            var ratio = moles / totalMoles;
            ratios[i] = ratio;

            if (_gasDataCache[i] is not { } data) continue;

            knownGasRatioSum += ratio;
            targetMods += new Vector4(
                data.TemperatureScaleModificator,
                data.TemperatureProtectionModificator,
                data.EnergyScaleModificator,
                data.WasteOutputModificator
            ) * ratio;
        }

        var unknownRatio = MathF.Max(0f, 1f - knownGasRatioSum);
        if (unknownRatio > 0f)
            targetMods += Vector4.One * (sm.Comp.BaseModificator * unknownRatio);

        foreach (var reaction in _reactionsCache)
        {
            if (!IsReactionMatch(reaction, ratios))
                continue;

            targetMods *= reaction.ModifiersVector;

            var ev = new SupermatterGasReactionEvent(reaction, sm.Comp.AtmosGas, frameTime);
            RaiseLocalEvent(sm, ref ev);

            var effectArgs = new SupermatterGasReactionEffectArgs(sm, sm.Comp.AtmosGas, frameTime, totalMoles, EntityManager);
            foreach (var effect in reaction.Effects)
            {
                effect.Effect(effectArgs);
            }
        }

        ApplyModifiersLerp(sm.Comp, targetMods, sm.Comp.ModificatorDecayRate * frameTime);
        ThrowUp(sm);
    }

    private bool IsReactionMatch(SupermatterReactionPrototype mix, ReadOnlySpan<float> ratios)
    {
        foreach (var (gas, targetRatio) in mix.Composition)
        {
            if (MathF.Abs(ratios[(int)gas] - targetRatio) > mix.Tolerance)
                return false;
        }

        for (var i = 0; i < ratios.Length; i++)
        {
            if (mix.Composition.ContainsKey((Gas)i)) continue;
            if (ratios[i] > mix.Tolerance)
                return false;
        }

        return true;
    }

    private static void DecayModificators(SupermatterComponent comp, float frameTime)
    {
        ApplyModifiersLerp(comp, Vector4.One * comp.BaseModificator, comp.ModificatorDecayRate * frameTime);
    }

    private static void ApplyModifiersLerp(SupermatterComponent comp, Vector4 target, float rate)
    {
        var step = MathF.Max(0f, MathF.Min(rate, 1f));

        comp.TemperatureScaleModificator = MathHelper.CloseTo(comp.TemperatureScaleModificator, target.X, 0.001f)
            ? target.X : MathHelper.Lerp(comp.TemperatureScaleModificator, target.X, step);

        comp.TemperatureProtectionModificator = MathHelper.CloseTo(comp.TemperatureProtectionModificator, target.Y, 0.001f)
            ? target.Y : MathHelper.Lerp(comp.TemperatureProtectionModificator, target.Y, step);

        comp.EnergyScaleModificator = MathHelper.CloseTo(comp.EnergyScaleModificator, target.Z, 0.001f)
            ? target.Z : MathHelper.Lerp(comp.EnergyScaleModificator, target.Z, step);

        comp.WasteOutputModificator = MathHelper.CloseTo(comp.WasteOutputModificator, target.W, 0.001f)
            ? target.W : MathHelper.Lerp(comp.WasteOutputModificator, target.W, step);
    }
}
