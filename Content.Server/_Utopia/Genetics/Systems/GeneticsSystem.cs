using System.Linq;
using Content.Server._Utopia.Genetics.Components;
using Content.Server._Utopia.Genetics.Mutations.Systems;
using Content.Server.Popups;
using Content.Shared._Utopia.Genetics;
using Content.Shared._Utopia.Genetics.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Systems;

public sealed partial class GeneticsSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly GeneticShuffleSystem _shuffle = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string DamageType = "Radiation";
    private const float MinSequenceRevealFraction = 0.45f;
    private const float MaxSequenceRevealFraction = 0.80f;
    private const float MinRadsUntilMutation = 20f;
    private const float MaxRadsUntilMutation = 95f;
    private const int InstabilityMutationThreshold = 100;
    private const int InstabilityDamageThreshold = 150;
    private const int MinInstabilityTimerSeconds = 90;
    private const int MaxInstabilityTimerSeconds = 160;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneticsComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<GeneticsComponent, DamageChangedEvent>(OnRadiationDamage);
    }

    private void OnInit(Entity<GeneticsComponent> ent, ref ComponentInit args)
    {
        FillBaseMutations(ent);
        ent.Comp.RadsUntilRandomMutation = _random.NextFloat(MinRadsUntilMutation, MaxRadsUntilMutation);
    }

    private void OnRadiationDamage(Entity<GeneticsComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        if (!delta.DamageDict.TryGetValue(DamageType, out var typeDamage) || typeDamage <= FixedPoint2.Zero)
            return;

        if (TryComp<MobStateComponent>(ent.Owner, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        ent.Comp.RadsUntilRandomMutation -= typeDamage.Float();

        if (ent.Comp.RadsUntilRandomMutation > 0)
            return;

        TriggerRandomMutation(ent);
        ent.Comp.RadsUntilRandomMutation = _random.NextFloat(MinRadsUntilMutation, MaxRadsUntilMutation);
    }

    public void FillBaseMutations(Entity<GeneticsComponent> ent)
    {
        ent.Comp.Mutations.Clear();
        ent.Comp.BaseMutationIds.Clear();

        var mutationsToAdd = new List<MutationEntry>();
        var addedForcedCount = 0;

        foreach (var forced in ent.Comp.ForcedBaseMutations)
        {
            if (!(_random.NextFloat() < forced.Chance))
                continue;

            if (!_proto.TryIndex<GeneticMutationPrototype>(forced.Id, out var proto)
            || !CanEntityReceiveMutation(ent, proto))
                continue;

            var slot = _shuffle.GetOrAssignSlot(forced.Id);
            if (slot.Block <= 0)
                continue;

            var startsActive = _random.Prob(forced.StartActive);
            var revealed = startsActive ? slot.Sequence : RandomizeSequence(slot.Sequence);

            var entry = CreateMutationEntry(forced.Id, proto, slot.Block, slot.Sequence, revealed, startsActive);

            mutationsToAdd.Add(entry);
            ent.Comp.BaseMutationIds.Add(forced.Id);

            if (startsActive)
            {
                ApplyMutationComponents(ent.Owner, proto);
            }

            addedForcedCount++;
        }

        var slotsLeft = Math.Max(0, ent.Comp.MutationSlots - addedForcedCount);

        for (var i = 0; i < slotsLeft; i++)
        {
            var chosenId = PickRandomAvailableMutation(ent);
            if (chosenId == null)
                break;

            if (!_proto.TryIndex<GeneticMutationPrototype>(chosenId, out var proto))
                continue;

            var slot = _shuffle.GetOrAssignSlot(chosenId);
            if (slot.Block <= 0)
                continue;

            var revealed = RandomizeSequence(slot.Sequence);
            var entry = CreateMutationEntry(
                chosenId, proto, slot.Block, slot.Sequence, revealed, false);

            mutationsToAdd.Add(entry);
            ent.Comp.BaseMutationIds.Add(chosenId);
        }

        _random.Shuffle(mutationsToAdd);

        foreach (var entry in mutationsToAdd)
        {
            ent.Comp.Mutations.Add(entry);
        }
    }

    private string? PickRandomAvailableMutation(Entity<GeneticsComponent> ent)
    {
        var candidates = _proto.EnumeratePrototypes<GeneticMutationPrototype>()
            .Where(p => CanEntityReceiveMutation(ent.Owner, p, true))
            .Where(p => !ent.Comp.Mutations.Any(m => m.Id == p.ID))
            .Where(p => !IsConflictingWithExisting(ent.Comp, p))
            .Where(p => !ent.Comp.BaseMutationIds.Contains(p.ID))
            .ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0].ID;

        var totalWeight = 0f;
        foreach (var proto in candidates)
        {
            totalWeight += proto.ProbabilityWeight;
        }

        var roll = _random.NextFloat(0f, totalWeight);

        var current = 0f;
        foreach (var proto in candidates)
        {
            current += proto.ProbabilityWeight;

            if (roll <= current)
                return proto.ID;
        }

        return candidates.Last().ID;
    }

    public void TriggerRandomMutation(Entity<GeneticsComponent> ent)
    {
        var chosenId = PickRandomAvailableMutation(ent);
        if (chosenId == null)
            return;

        var slot = _shuffle.GetOrAssignSlot(chosenId);
        if (slot.Block <= 0)
            return;

        TryAddMutation(ent, chosenId);
        TryActivateMutation(ent, chosenId);
    }

    public void RemoveRandomMutation(Entity<GeneticsComponent> ent, bool mutadone = false)
    {
        var removable = new List<string>();

        foreach (var entry in ent.Comp.Mutations)
        {
            if (mutadone)
            {
                var proto = _proto.Index<GeneticMutationPrototype>(entry.Id);
                if (proto.MutadoneResistant)
                    continue;
            }

            if (!ent.Comp.BaseMutationIds.Contains(entry.Id))
            {
                removable.Add(entry.Id);
                continue;
            }

            if (entry.Enabled)
            {
                removable.Add(entry.Id);
            }
        }

        if (removable.Count == 0)
            return;

        var chosenId = _random.Pick(removable);

        TryRemoveMutation(ent, chosenId);
    }

    private string RandomizeSequence(string original)
    {
        if (string.IsNullOrEmpty(original) || original.Length <= 2)
            return original;

        var length = original.Length;
        var result = new char[length];

        result[0] = original[0];
        result[length - 1] = original[length - 1];

        var revealCount = _random.Next((int)(length * MinSequenceRevealFraction),
            (int)(length * MaxSequenceRevealFraction) + 1);

        var revealedPositions = new HashSet<int>
        {
            0,
            length - 1
        };

        while (revealedPositions.Count < revealCount)
        {
            revealedPositions.Add(_random.Next(1, length - 1));
        }

        for (var i = 1; i < length - 1; i++)
        {
            result[i] = revealedPositions.Contains(i) ? original[i] : 'X';
        }

        return new string(result);
    }

    public bool TryAddMutation(Entity<GeneticsComponent> ent, string mutationId)
    {
        var slot = _shuffle.GetOrAssignSlot(mutationId);

        if (slot == GeneticBlock.Invalid
        || ent.Comp.Mutations.Any(m => m.Id == mutationId)
        || !_proto.TryIndex(mutationId, out GeneticMutationPrototype? proto))
            return false;

        if (!CanEntityReceiveMutation(ent.Owner, proto, false))
            return false;

        if (IsConflictingWithExisting(ent.Comp, proto))
            return false;

        var revealed = RandomizeSequence(slot.Sequence);
        var entry = CreateMutationEntry(
            mutationId: mutationId,
            proto: proto,
            block: slot.Block,
            originalSequence: slot.Sequence,
            revealedSequence: revealed,
            enabled: false
        );

        ent.Comp.Mutations.Add(entry);

        if (!ent.Comp.BaseMutationIds.Contains(mutationId))
        {
            ModifyInstability(ent, proto.Instability);
        }

        return true;
    }

    private bool TryRemoveMutation(Entity<GeneticsComponent> ent, string mutationId)
    {
        var entry = ent.Comp.Mutations.Find(m => m.Id == mutationId);
        if (entry == null)
            return false;

        var isBase = ent.Comp.BaseMutationIds.Contains(mutationId);

        if (entry.Enabled)
        {
            if (!TryDeactivateMutation(ent, mutationId))
                return false;
        }

        if (isBase)
        {
            return true;
        }

        if (_proto.TryIndex<GeneticMutationPrototype>(mutationId, out var proto))
        {
            ModifyInstability(ent, -proto.Instability);
        }

        ent.Comp.Mutations.Remove(entry);

        return true;
    }

    public bool TryActivateMutation(Entity<GeneticsComponent> ent, string mutationId)
    {
        var index = ent.Comp.Mutations.FindIndex(m => m.Id == mutationId);
        if (index == -1)
            return false;

        var entry = ent.Comp.Mutations[index];
        if (entry.Enabled)
            return false;

        if (!_proto.TryIndex(mutationId, out GeneticMutationPrototype? proto))
            return false;

        if (!CanEntityReceiveMutation(ent.Owner, proto, false))
            return false;

        foreach (var conflictId in proto.Conflicts)
        {
            if (ent.Comp.Mutations.Any(m => m.Id == conflictId && m.Enabled))
                return false;
        }

        ent.Comp.Mutations[index] = entry with
        {
            Enabled = true,
            RevealedSequence = entry.OriginalSequence
        };

        ApplyMutationComponents(ent.Owner, proto);

        var popMsg = !string.IsNullOrWhiteSpace(proto.PopupText)
            ? Loc.GetString(proto.PopupText)
            : Loc.GetString("genetics-mutation-activated");

        _popup.PopupEntity(popMsg, ent.Owner, ent.Owner);

        return true;
    }

    public bool TryDeactivateMutation(Entity<GeneticsComponent> ent, string mutationId)
    {
        for (var i = 0; i < ent.Comp.Mutations.Count; i++)
        {
            if (ent.Comp.Mutations[i].Id != mutationId)
                continue;

            if (!ent.Comp.Mutations[i].Enabled)
                return false;

            if (!_proto.TryIndex<GeneticMutationPrototype>(mutationId, out var proto))
                return false;

            ent.Comp.Mutations[i] = ent.Comp.Mutations[i] with { Enabled = false };
            RemoveMutationComponents(ent.Owner, proto);
            return true;
        }

        return false;
    }

    private void ApplyMutationComponents(EntityUid uid, GeneticMutationPrototype proto)
    {
        EntityManager.AddComponents(uid, proto.Components);
    }

    private void RemoveMutationComponents(EntityUid uid, GeneticMutationPrototype proto)
    {
        foreach (var (comp, _) in proto.Components.Values)
        {
            RemCompDeferred(uid, comp.GetType());
        }
    }

    private bool IsConflictingWithExisting(GeneticsComponent component, GeneticMutationPrototype proto)
    {
        return proto.Conflicts.Any(conflictId =>
            component.Mutations.Any(m => m.Id == conflictId));
    }

    private void ModifyInstability(Entity<GeneticsComponent> ent, int delta)
    {
        if (delta == 0)
            return;

        var old = ent.Comp.GeneticInstability;
        ent.Comp.GeneticInstability += delta;
        var newVal = ent.Comp.GeneticInstability;

        if (old <= InstabilityMutationThreshold && newVal > InstabilityMutationThreshold)
        {
            RemComp<PendingInstabilityMutationComponent>(ent.Owner);

            var pending = AddComp<PendingInstabilityMutationComponent>(ent.Owner);
            pending.MutationId = string.Empty;

            var durationSeconds = _random.Next(MinInstabilityTimerSeconds, MaxInstabilityTimerSeconds);
            pending.EndTime = _timing.CurTime + TimeSpan.FromSeconds(durationSeconds);
        }

        else if (old >= InstabilityMutationThreshold && newVal < InstabilityMutationThreshold)
        {
            if (RemComp<PendingInstabilityMutationComponent>(ent.Owner))
            {
                _popup.PopupEntity(Loc.GetString("genetics-instability-cancelled"), ent.Owner, ent.Owner);
            }
        }

        if (old <= InstabilityDamageThreshold && newVal > InstabilityDamageThreshold)
        {
            EnsureComp<GeneticsInstabilityDamageComponent>(ent.Owner);
        }

        else if (old > InstabilityDamageThreshold && newVal <= InstabilityDamageThreshold)
        {
            RemComp<GeneticsInstabilityDamageComponent>(ent.Owner);
        }
    }

    private bool IsPrototypeOrParentInList(string entityProtoId, IReadOnlyList<string> list)
    {
        if (list.Contains(entityProtoId))
            return true;

        if (!_proto.TryIndex<EntityPrototype>(entityProtoId, out var proto) || proto.Parents == null)
            return false;

        return proto.Parents.Any(parent => IsPrototypeOrParentInList(parent, list));
    }

    public bool CanEntityReceiveMutation(EntityUid uid, GeneticMutationPrototype proto, bool isRandom = true)
    {
        var protoId = MetaData(uid).EntityPrototype?.ID ?? "Unknown";

        if (proto.StrictEntityWhitelist != null && proto.StrictEntityWhitelist.Count > 0)
        {
            if (!IsPrototypeOrParentInList(protoId, proto.StrictEntityWhitelist))
                return false;
        }

        if (proto.StrictEntityBlacklist != null && proto.StrictEntityBlacklist.Count > 0)
        {
            if (IsPrototypeOrParentInList(protoId, proto.StrictEntityBlacklist))
                return false;
        }

        if (isRandom && proto.Hidden)
            return false;

        if (isRandom)
        {
            if (proto.EntityWhitelist != null && proto.EntityWhitelist.Count > 0)
            {
                if (!IsPrototypeOrParentInList(protoId, proto.EntityWhitelist))
                    return false;
            }

            if (proto.EntityBlacklist != null && proto.EntityBlacklist.Count > 0)
            {
                if (IsPrototypeOrParentInList(protoId, proto.EntityBlacklist))
                    return false;
            }
        }

        return true;
    }

    public bool TryModifyMutationSequence(EntityUid uid, GeneticsComponent component, string mutationId, int index, char newBase)
    {
        var entryIndex = component.Mutations.FindIndex(m => m.Id == mutationId);
        if (entryIndex == -1)
            return false;

        var entry = component.Mutations[entryIndex];

        var proto = _proto.Index<GeneticMutationPrototype>(mutationId);
        if (proto.SequencerResistant)
            return false;

        if (index < 0 || index >= entry.RevealedSequence.Length)
            return false;

        var newSeq = entry.RevealedSequence.ToCharArray();
        newSeq[index] = char.ToUpper(newBase);

        component.Mutations[entryIndex] = entry with { RevealedSequence = new string(newSeq) };
        return true;
    }

    private MutationEntry CreateMutationEntry(string mutationId, GeneticMutationPrototype proto, int block, string originalSequence,
        string revealedSequence, bool enabled)
    {
        return new MutationEntry(
            block,
            mutationId,
            proto.Name,
            originalSequence,
            revealedSequence,
            enabled,
            proto.Description,
            proto.Instability,
            proto.Conflicts
        );
    }

    public void ScrambleDna(Entity<GeneticsComponent> ent)
    {
        var preservedEntries = new List<MutationEntry>();

        foreach (var entry in ent.Comp.Mutations)
        {
            if (!_proto.TryIndex<GeneticMutationPrototype>(entry.Id, out var proto))
                continue;

            if (proto.ScrambleResistant)
                preservedEntries.Add(entry);
        }

        foreach (var entry in ent.Comp.Mutations.ToList())
        {
            if (!entry.Enabled)
                continue;

            if (!_proto.TryIndex<GeneticMutationPrototype>(entry.Id, out var proto) || !proto.ScrambleResistant)
            {
                TryDeactivateMutation(ent, entry.Id);
            }
        }

        ent.Comp.Mutations.Clear();
        ent.Comp.BaseMutationIds.Clear();

        ent.Comp.GeneticInstability = 0;

        FillBaseMutations(ent);

        foreach (var preserved in preservedEntries)
        {
            if (TryAddMutation(ent, preserved.Id))
            {
                var index = ent.Comp.Mutations.FindIndex(m => m.Id == preserved.Id);
                if (index != -1)
                {
                    var newEntry = ent.Comp.Mutations[index];

                    ent.Comp.Mutations[index] = newEntry with
                    {
                        RevealedSequence = preserved.RevealedSequence,
                        Enabled = preserved.Enabled
                    };
                }
            }
        }

        return;
    }
}
