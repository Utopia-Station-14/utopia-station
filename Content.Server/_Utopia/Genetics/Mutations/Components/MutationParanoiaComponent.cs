namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationParanoiaComponent : Component
{
    [DataField]
    public float Interval = 60.0f;

    [DataField]
    public float EmoteChance = 0.6f;

    [DataField]
    public string EmotionId = "Scream";

    [ViewVariables]
    public TimeSpan NextCheck;
}
