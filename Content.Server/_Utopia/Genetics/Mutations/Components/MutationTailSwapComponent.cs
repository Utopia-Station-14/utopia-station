namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationTailSwapComponent : Component
{
    [DataField(required: true)]
    public string NewTailMarking = default!;

    [DataField]
    public Color? TailColor;

    public List<(string MarkingId, List<Color> Colors)>? OriginalTailMarkings { get; set; }
}
