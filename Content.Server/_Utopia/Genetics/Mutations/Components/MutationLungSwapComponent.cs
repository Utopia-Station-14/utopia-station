namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationLungSwapComponent : Component
{
    [DataField(required: true)]
    public string NewLungPrototype = default!;

    [DataField]
    public string HiddenStorageContainerId = "mutation_hidden_lung_storage";

    public EntityUid? OriginalLung { get; set; }

    public EntityUid? SwappedLung { get; set; }
}
