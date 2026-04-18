using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Utopia.Genetics.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class DnaScannerConsoleComponent : Component
{
    /// <summary>
    /// Currently scanned entity
    /// </summary>
    [DataField]
    public EntityUid? CurrentSubject;

    /// <summary>
    /// Mutations saved in the console's storage
    /// </summary>
    [DataField]
    public List<MutationEntry> SavedMutations = new();

    /// <summary>
    /// Mutations currently being researched in this console.
    /// </summary>
    [DataField]
    public HashSet<string> ActiveResearchQueue = new();

    /// <summary>
    /// Last time we processed a research tick (once per second).
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? LastResearchTick;

    /// <summary>
    /// Number of available DNA injectors in storage
    /// </summary>
    [DataField]
    public int DnaInjectors = 60;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? ScrambleCooldownEnd;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? JokerCooldownEnd;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextHealthUpdate;

    [DataField]
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier SoundDnaScramble = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}

[Serializable, NetSerializable]
public sealed class GeneticistsConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public string? SubjectName;

    public string? HealthStatus;

    public float? RadiationDamage;

    public int SubjectGeneticInstability;

    public TimeSpan? ScrambleCooldownEnd;

    public List<MutationEntry>? Mutations;

    public HashSet<string> DiscoveredMutationIds = new();

    public HashSet<string> BaseMutationIds = new();

    public List<MutationEntry> SavedMutations = new();

    public bool IsFullUpdate;

    public Dictionary<string, int> ResearchRemaining = new();

    public Dictionary<string, int> ResearchOriginal = new();

    public HashSet<string> ActiveResearchMutationIds = new();

    public TimeSpan? JokerCooldownEnd;

    public GeneticistsConsoleBoundUserInterfaceState(
        string? subjectName = null,
        string? healthStatus = null,
        float? radiationDamage = null,
        int subjectGeneticInstability = 0,
        TimeSpan? scrambleCooldownEnd = null,
        List<MutationEntry>? mutations = null,
        HashSet<string>? discoveredMutationIds = null,
        HashSet<string>? baseMutationIds = null,
        List<MutationEntry>? savedMutations = null,
        bool isFullUpdate = true,
        Dictionary<string, int> researchRemaining = default!,
        Dictionary<string, int> researchOriginal = default!,
        HashSet<string>? activeResearchMutationIds = null,
        TimeSpan? jokerCooldownEnd = null)
    {
        SubjectName = subjectName;
        HealthStatus = healthStatus;
        RadiationDamage = radiationDamage;
        SubjectGeneticInstability = subjectGeneticInstability;
        ScrambleCooldownEnd = scrambleCooldownEnd;
        Mutations = mutations;
        DiscoveredMutationIds = discoveredMutationIds ?? new();
        BaseMutationIds = baseMutationIds ?? new();
        SavedMutations = savedMutations ?? new();
        IsFullUpdate = isFullUpdate;
        ResearchRemaining = researchRemaining;
        ResearchOriginal = researchOriginal;
        ActiveResearchMutationIds = activeResearchMutationIds ?? new();
        JokerCooldownEnd = jokerCooldownEnd;
    }
}

[Serializable, NetSerializable]
public enum DnaScannerConsoleUiKey : byte
{
    Key
}
