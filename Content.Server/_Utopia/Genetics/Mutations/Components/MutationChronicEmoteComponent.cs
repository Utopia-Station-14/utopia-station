namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationChronicEmoteComponent : Component
{
    [DataField]
    public float Interval = 15.0f;

    [DataField]
    public float EmoteChance = 0.4f;

    [DataField]
    public float DropChance = 0.25f;

    [DataField(required: true)]
    public string EmoteId = "Cough";

    [ViewVariables]
    public TimeSpan NextCheck;
}
