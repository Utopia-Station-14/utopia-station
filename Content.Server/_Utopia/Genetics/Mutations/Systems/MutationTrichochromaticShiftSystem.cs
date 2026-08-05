using System.Numerics;
using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Humanoid;
using Content.Shared._Utopia.Genetics.Events;
using Content.Shared.Actions;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationTrichochromaticShiftSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationTrichochromaticShiftComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MutationTrichochromaticShiftComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MutationTrichochromaticShiftComponent, TrichochromaticShiftActionEvent>(OnActivate);
    }

    private void OnStartup(Entity<MutationTrichochromaticShiftComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.GrantedAction, ent.Comp.ActionId);

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        var originalHair = new List<(string, List<Color>)>();
        var originalFacial = new List<(string, List<Color>)>();

        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Hair, out var hairMarkings))
        {
            foreach (var marking in hairMarkings)
            {
                originalHair.Add((marking.MarkingId, new List<Color>(marking.MarkingColors)));
            }
        }

        if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.FacialHair, out var facialMarkings))
        {
            foreach (var marking in facialMarkings)
            {
                originalFacial.Add((marking.MarkingId, new List<Color>(marking.MarkingColors)));
            }
        }

        ent.Comp.OriginalHairMarkings = originalHair;
        ent.Comp.OriginalFacialHairMarkings = originalFacial;
        ent.Comp.UsesSinceOriginal = 0;
    }

    private void OnShutdown(Entity<MutationTrichochromaticShiftComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.GrantedAction is { Valid: true } action)
        {
            _actions.RemoveAction(action);
        }

        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        humanoid.MarkingSet.RemoveCategory(MarkingCategories.Hair);
        humanoid.MarkingSet.RemoveCategory(MarkingCategories.FacialHair);

        if (ent.Comp.OriginalHairMarkings is { } hair)
        {
            foreach (var (id, colors) in hair)
            {
                _humanoid.AddMarking(ent.Owner, id, colors, forced: true);
            }
        }

        if (ent.Comp.OriginalFacialHairMarkings is { } facial)
        {
            foreach (var (id, colors) in facial)
            {
                _humanoid.AddMarking(ent.Owner, id, colors, forced: true);
            }
        }
    }

    private void OnActivate(Entity<MutationTrichochromaticShiftComponent> ent, ref TrichochromaticShiftActionEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        ent.Comp.UsesSinceOriginal = (ent.Comp.UsesSinceOriginal + 1) % 4;

        if (ent.Comp.UsesSinceOriginal == 3)
        {
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.Hair);
            humanoid.MarkingSet.RemoveCategory(MarkingCategories.FacialHair);

            if (ent.Comp.OriginalHairMarkings is { } hair)
            {
                foreach (var (id, colors) in hair)
                {
                    _humanoid.AddMarking(ent.Owner, id, colors, forced: true);
                }
            }

            if (ent.Comp.OriginalFacialHairMarkings is { } facial)
            {
                foreach (var (id, colors) in facial)
                {
                    _humanoid.AddMarking(ent.Owner, id, colors, forced: true);
                }
            }
        }
        else
        {
            var hue = _random.NextFloat(0f, 1f);
            var saturation = _random.NextFloat(0f, 1f);
            var value = _random.NextFloat(0f, 1f);
            var randomColor = Color.FromHsv(new Vector4(hue, saturation, value, 1f));

            if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Hair, out var hairMarkings))
            {
                for (var i = 0; i < hairMarkings.Count; i++)
                {
                    _humanoid.SetMarkingColor(ent.Owner, MarkingCategories.Hair, i, new List<Color> { randomColor });
                }
            }

            if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.FacialHair, out var facialMarkings))
            {
                for (var i = 0; i < facialMarkings.Count; i++)
                {
                    _humanoid.SetMarkingColor(ent.Owner, MarkingCategories.FacialHair, i, new List<Color> { randomColor });
                }
            }
        }
    }
}
