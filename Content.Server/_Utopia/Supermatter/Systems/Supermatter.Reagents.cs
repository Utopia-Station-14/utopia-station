
using System.Numerics;
using Content.Shared._Utopia.Supermatter.Events;
using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared._Utopia.Supermatter.Prototypes;
using Content.Shared.Chemistry.Reagent;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    public void AddReagents(EntityUid sm, SupermatterComponent comp, IReadOnlyList<ReagentQuantity>? reagents)
    {
        // if (reagents == null)
        //     return;

        // if (!_solutionContainer.ResolveSolution(sm.Owner, sm.Comp.SolutionName, ref sm.Comp.Solution, out var solution))
        //     return;

        // foreach (var reagent in reagents)
        //     _solutionContainer.TryAddReagent(comp.Solution.Value, reagent, out _);
    }

    public void ProcessReagents(Entity<SupermatterComponent> sm, float frameTime)
    {
        var comp = sm.Comp;

        if (!_solutionContainer.ResolveSolution(sm.Owner, comp.SolutionName, ref comp.Solution, out var solution) || solution.Volume == 0)
        {
            DecayReagentModificators(comp, frameTime);
            return;
        }

        var totalVolume = solution.Volume.Float();
        var knownRatioSum = 0f;
        var targetMods = Vector4.Zero;

        var currentRatios = new Dictionary<string, float>();

        foreach (var reagent in solution.Contents)
        {
            var reagentId = reagent.Reagent.Prototype;
            var ratio = reagent.Quantity.Float() / totalVolume;
            currentRatios[reagentId] = ratio;

            if (!_reagentDataCache.TryGetValue(reagentId, out var data))
                continue;

            knownRatioSum += ratio;
            targetMods += new Vector4(
                data.TemperatureScaleModificator,
                data.TemperatureProtectionModificator,
                data.EnergyScaleModificator,
                data.WasteOutputModificator
            ) * ratio;
        }

        var unknownRatio = Math.Max(0f, 1f - knownRatioSum);
        if (unknownRatio > 0f)
            targetMods += Vector4.One * (comp.BaseModificator * unknownRatio);

        foreach (var reaction in _reagentReactionsCache)
        {
            if (!IsReagentReactionMatch(reaction, currentRatios))
                continue;

            targetMods *= reaction.ModifiersVector;

            var ev = new SupermatterReagentReactionEvent(reaction, currentRatios, frameTime);
            RaiseLocalEvent(sm, ref ev);
        }

        ApplyReagentModifiersLerp(comp, targetMods, comp.ModificatorDecayRate * frameTime);

        if (comp.Solution != null)
        {
            _solutionContainer.RemoveAllSolution(comp.Solution.Value);
        }
    }

    private bool IsReagentReactionMatch(SupermatterReagentReactionPrototype mix, Dictionary<string, float> currentRatios)
    {
        foreach (var (reagentId, targetRatio) in mix.Composition)
        {
            if (!currentRatios.TryGetValue(reagentId, out var currentRatio))
                return false;

            if (Math.Abs(currentRatio - targetRatio) > mix.Tolerance)
                return false;
        }

        foreach (var (reagentId, currentRatio) in currentRatios)
        {
            if (mix.Composition.ContainsKey(reagentId))
                continue;

            if (currentRatio > mix.Tolerance)
                return false;
        }

        return true;
    }

    private static void DecayReagentModificators(SupermatterComponent comp, float frameTime)
    {
        ApplyReagentModifiersLerp(comp, Vector4.One * comp.BaseModificator, comp.ModificatorDecayRate * frameTime);
    }

    private static void ApplyReagentModifiersLerp(SupermatterComponent comp, Vector4 target, float rate)
    {
        var step = Math.Clamp(rate, 0f, 1f);

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
