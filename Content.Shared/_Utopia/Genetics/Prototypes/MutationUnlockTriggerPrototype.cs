using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Genetics.Prototypes;

[Prototype]
public sealed partial class MutationUnlockTriggerPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<string> RequiredMutations { get; private set; } = new();

    [DataField(required: true)]
    public List<string> UnlockMutations { get; private set; } = new();
}
