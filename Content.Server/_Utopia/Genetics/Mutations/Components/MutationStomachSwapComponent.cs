namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationStomachSwapComponent : Component
{
    [DataField(required: true)]
    public string NewStomachPrototype = default!;

    [DataField]
    public string HiddenStorageContainerId = "mutation_hidden_stomach_storage";

    public EntityUid? OriginalStomach { get; set; }

    public EntityUid? SwappedStomach { get; set; }
}
