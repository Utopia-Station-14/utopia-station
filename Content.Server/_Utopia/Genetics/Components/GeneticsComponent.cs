using Content.Shared._Utopia.Genetics;

namespace Content.Server._Utopia.Genetics.Components;

[RegisterComponent, AutoGenerateComponentState]
public sealed partial class GeneticsComponent : Component
{
    [DataField]
    public int MutationSlots = 2;

    [ViewVariables(VVAccess.ReadOnly)]
    public List<MutationEntry> Mutations = new();

    [DataField]
    public HashSet<string> BaseMutationIds = new();

    [DataField("baseMutations")]
    public List<ForcedMutation> ForcedBaseMutations = new();

    [DataField, AutoNetworkedField]
    public int GeneticInstability = 0;

    [ViewVariables, AutoNetworkedField]
    public float RadsUntilRandomMutation = 50f;
}

[DataDefinition, Serializable]
public sealed partial class ForcedMutation
{
    [DataField(required: true)]
    public string Id { get; set; } = default!;

    [DataField]
    public float StartActive { get; set; } = 1f;

    [DataField]
    public float Chance { get; set; } = 1f;
}

