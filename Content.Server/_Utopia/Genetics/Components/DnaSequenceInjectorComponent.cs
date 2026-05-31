using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server._Utopia.Genetics.Components;

[RegisterComponent]
public sealed partial class DnaSequenceInjectorComponent : Component
{
    [DataField]
    public string? MutationId;

    [DataField]
    public bool IsMutator = false;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string EntityEmpty = "UtopiaDNAInjectorEmpty";
}
