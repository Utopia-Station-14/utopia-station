using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Content.Shared._Utopia.Genetics.Systems;
using Robust.Shared.Audio;

namespace Content.Shared._Utopia.Genetics.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedGeneticAnalyzerSystem))]
public sealed partial class GeneticAnalyzerComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? PatientName;

    [DataField, AutoNetworkedField]
    public int PatientInstability;

    [DataField, AutoNetworkedField]
    public List<MutationEntry> Mutations = new();

    [DataField]
    public string ReportEntity = "GeneticAnalyzerReportPaper";

    [DataField]
    public SoundSpecifier ScanningEndSound = new SoundPathSpecifier("/Audio/Items/Medical/healthscanner.ogg");
}


[Serializable, NetSerializable]
public enum GeneticAnalyzerUiKey : byte
{
    Key
}

[NetSerializable, Serializable]
public sealed class GeneticAnalyzerUiState(
    string? patientName,
    int patientInstability,
    List<MutationEntry> mutations,
    HashSet<string> discoveredIds) : BoundUserInterfaceState
{
    public string? PatientName = patientName;
    public int PatientInstability = patientInstability;
    public List<MutationEntry> Mutations = mutations;
    public HashSet<string> DiscoveredIds = discoveredIds;
}
