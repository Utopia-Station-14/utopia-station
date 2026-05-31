using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationTrichochromaticShiftComponent : Component
{
    [DataField]
    public EntProtoId ActionId = "ActionTrichochromaticShift";

    public EntityUid? GrantedAction;

    public List<(string MarkingId, List<Color> Colors)>? OriginalHairMarkings { get; set; }

    public List<(string MarkingId, List<Color> Colors)>? OriginalFacialHairMarkings { get; set; }

    public int UsesSinceOriginal { get; set; } = 0;
}
