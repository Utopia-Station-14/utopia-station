using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationTailSwapSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationTailSwapComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MutationTailSwapComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid ent, MutationTailSwapComponent comp, ref ComponentStartup args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        var originalTailMarkings = new List<(string, List<Color>)>();
        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var currentTails))
        {
            foreach (var marking in currentTails)
            {
                var colors = new List<Color>();

                for (var i = 0; i < marking.MarkingColors.Count; i++)
                {
                    colors.Add(marking.MarkingColors[i]);
                }

                originalTailMarkings.Add((marking.MarkingId, colors));
            }
        }

        comp.OriginalTailMarkings = originalTailMarkings;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Tail);

        Color tailColor;

        if (comp.TailColor is { } customColor)
        {
            tailColor = customColor;
        }
        else
        {
            tailColor = humanoid.SkinColor;
        }

        if (_proto.TryIndex<MarkingPrototype>(comp.NewTailMarking, out var markingProto))
        {
            var spriteCount = markingProto.Sprites.Count;
            var colors = new List<Color>();

            for (var i = 0; i < spriteCount; i++)
            {
                colors.Add(tailColor);
            }

            _humanoid.AddMarking(ent, comp.NewTailMarking, colors, forced: true);
        }
        else
        {
            _humanoid.AddMarking(ent, comp.NewTailMarking, forced: true);
        }

        Dirty(ent, humanoid);
    }

    private void OnShutdown(Entity<MutationTailSwapComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.MarkingSet.Remove(MarkingCategories.Tail, ent.Comp.NewTailMarking);

        if (ent.Comp.OriginalTailMarkings is { } originals)
        {
            foreach (var (markingId, colors) in originals)
            {
                _humanoid.AddMarking(ent, markingId, colors, forced: true);
            }
        }

        Dirty(ent, humanoid);
    }
}
