using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationRadioComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<RadioChannelPrototype>> RadioChannels = new();

    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> ActiveAddedChannels = new();

    [DataField]
    public HashSet<ProtoId<RadioChannelPrototype>> TransmitterAddedChannels = new();
}
