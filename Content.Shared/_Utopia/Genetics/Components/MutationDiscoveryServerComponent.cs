using Content.Shared._Utopia.Genetics.Systems;

namespace Content.Shared._Utopia.Genetics.Components;

[RegisterComponent, Access(typeof(SharedMutationDiscoverySystem))]
public sealed partial class DnaScannerDiscoveryTrackerComponent : Component
{
    [DataField]
    public HashSet<string> GridDiscoveredMutations = new();

    [DataField]
    public Dictionary<string, int> GridResearchProgress = new();
}
