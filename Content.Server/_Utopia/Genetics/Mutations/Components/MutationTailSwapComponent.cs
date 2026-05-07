namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationTailSwapComponent : Component
{
    /// <summary>
    /// The marking ID of the tail to apply
    /// </summary>
    [DataField(required: true)]
    public string NewTailMarking = default!;

    /// <summary>
    /// Custom color for the new tail. If null, will use the entity's skin color.
    /// </summary>
    [DataField]
    public Color? TailColor;

    public List<(string MarkingId, List<Color> Colors)>? OriginalTailMarkings { get; set; }
}
