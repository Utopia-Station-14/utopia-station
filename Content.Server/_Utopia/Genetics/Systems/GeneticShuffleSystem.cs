using Content.Server.GameTicking.Events;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server._Utopia.Genetics.Systems;

public sealed partial class GeneticShuffleSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private const int MaxShuffledBlocks = 150;

    private static ReadOnlySpan<char> Bases => "ATGC";
    private static ReadOnlySpan<char> Pairs => "TACG";

    private readonly HashSet<string> _usedSequences = new();
    private readonly List<int> _shuffledBlocks = new();

    private Dictionary<string, GeneticBlock> _currentMapping = new();

    private int _nextSequentialBlock = 1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent _)
    {
        ResetForNextRound();
    }

    private void ResetForNextRound()
    {
        _currentMapping.Clear();
        _usedSequences.Clear();
        _shuffledBlocks.Clear();
        _shuffledBlocks.AddRange(Enumerable.Range(1, MaxShuffledBlocks));
        _random.Shuffle(_shuffledBlocks);

        _nextSequentialBlock = MaxShuffledBlocks + 1;
    }

    public bool TryGetSlot(string mutationId, out GeneticBlock slot)
    {
        return _currentMapping.TryGetValue(mutationId, out slot);
    }

    public bool HasSlot(string mutationId)
    {
        return _currentMapping.TryGetValue(mutationId, out _);
    }

    public GeneticBlock GetOrAssignSlot(string mutationId)
    {
        if (_currentMapping.TryGetValue(mutationId, out var existing))
            return existing;

        var sequence = GenerateUniqueSequence();

        var block = _shuffledBlocks.Count > 0
            ? _shuffledBlocks[^1]
            : _nextSequentialBlock++;

        if (_shuffledBlocks.Count > 0)
        {
            _shuffledBlocks.RemoveAt(_shuffledBlocks.Count - 1);
        }

        var blockData = new GeneticBlock(block, sequence);
        _currentMapping[mutationId] = blockData;

        return blockData;
    }

    private string GenerateUniqueSequence()
    {
        Span<char> buffer = stackalloc char[32];
        string seq;

        do
        {
            for (var i = 0; i < 32; i += 2)
            {
                var idx = _random.Next(4);
                buffer[i] = Bases[idx];
                buffer[i + 1] = Pairs[idx];
            }

            seq = new string(buffer);

        } while (!_usedSequences.Add(seq));

        return seq;
    }
}
