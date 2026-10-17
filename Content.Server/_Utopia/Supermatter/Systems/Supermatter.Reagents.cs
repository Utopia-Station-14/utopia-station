
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
        if (reagents == null)
            return;

        if (!_solutionContainer.ResolveSolution(sm, comp.SolutionName, ref comp.Solution, out var solution))
            return;

        foreach (var reagent in reagents)
            _solutionContainer.TryAddReagent(comp.Solution.Value, reagent, out _);
    }

    public void ProcessReagents(Entity<SupermatterComponent> sm, float frameTime)
    {
        var comp = sm.Comp;

        if (!_solutionContainer.ResolveSolution(sm.Owner, comp.SolutionName, ref comp.Solution, out var solution) || solution.Volume == 0)
        {
            DecayModifiers(comp, frameTime);
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
                data.TemperatureScaleModifier,
                data.TemperatureProtectionModifier,
                data.EnergyScaleModifier,
                data.WasteOutputModifier
            ) * ratio;
        }

        var unknownRatio = Math.Max(0f, 1f - knownRatioSum);
        if (unknownRatio > 0f)
            targetMods += Vector4.One * (comp.BaseModifier * unknownRatio);

        foreach (var reaction in _reagentReactionsCache)
        {
            if (!IsReagentReactionMatch(reaction, currentRatios))
                continue;

            targetMods *= reaction.ModifiersVector;

            var ev = new SupermatterReagentReactionEvent(reaction, currentRatios, frameTime);
            RaiseLocalEvent(sm, ref ev);
        }

        ApplyModifiersLerp(comp, targetMods, comp.ModifierDecayRate * frameTime);

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
}
