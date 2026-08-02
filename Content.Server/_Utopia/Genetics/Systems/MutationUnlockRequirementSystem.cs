using System.Linq;
using Content.Shared._Utopia.Genetics;
using Content.Shared._Utopia.Genetics.Systems;
using Content.Shared._Utopia.Genetics.Components;
using Content.Shared._Utopia.Genetics.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Systems;

public sealed class MutationUnlockTriggerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly GeneticShuffleSystem _shuffle = default!;
    [Dependency] private readonly SharedMutationDiscoverySystem _discovery = default!;

    private readonly List<MutationUnlockTriggerPrototype> _triggers = new();

    public override void Initialize()
    {
        base.Initialize();

        LoadTriggers();
        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.ByType.ContainsKey(typeof(MutationUnlockTriggerPrototype)))
        {
            LoadTriggers();
        }
    }

    private void LoadTriggers()
    {
        _triggers.Clear();
        _triggers.AddRange(_proto.EnumeratePrototypes<MutationUnlockTriggerPrototype>());
    }

    public void OnMutationSaved(EntityUid consoleUid, DnaScannerConsoleComponent console)
    {
        var savedIds = console.SavedMutations.Select(m => m.Id).ToHashSet();

        foreach (var trigger in _triggers)
        {
            if (!trigger.RequiredMutations.All(savedIds.Contains))
                continue;

            foreach (var unlockId in trigger.UnlockMutations)
            {
                if (console.SavedMutations.Any(m => m.Id == unlockId))
                    continue;

                if (!_proto.TryIndex<GeneticMutationPrototype>(unlockId, out var proto))
                    continue;

                var slot = _shuffle.GetOrAssignSlot(unlockId);

                if (slot.Block <= 0)
                    continue;

                var entry = new MutationEntry(
                    slot.Block,
                    unlockId,
                    proto.Name,
                    slot.Sequence,
                    slot.Sequence,
                    false,
                    proto.Description,
                    proto.Instability,
                    proto.Conflicts
                );

                _discovery.DiscoverMutation(consoleUid, unlockId);
            }
        }

        Dirty(consoleUid, console);
    }
}
