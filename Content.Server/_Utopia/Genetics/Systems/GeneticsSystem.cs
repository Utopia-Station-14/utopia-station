using System.Linq;
using Content.Server._Utopia.Genetics.Components;
using Content.Server.Popups;
using Content.Shared._Utopia.Genetics;
using Content.Shared._Utopia.Genetics.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Mobs;
using Content.Server._Utopia.Genetics.Mutations.Systems;
using Content.Shared._Utopia.Helpers;

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

    private void OnInit(EntityUid uid, GeneticsComponent component, ComponentInit args)
    {
        FillBaseMutations(uid, component);
        component.RadsUntilRandomMutation = _random.NextFloat(MinRadsUntilMutation, MaxRadsUntilMutation);
    }

    private void OnRadiationDamage(EntityUid uid, GeneticsComponent component, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        if (!delta.DamageDict.TryGetValue(DamageType, out var typeDamage) || typeDamage <= FixedPoint2.Zero)
            return;

        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
            return;

        component.RadsUntilRandomMutation -= typeDamage.Float();

        if (component.RadsUntilRandomMutation > 0)
            return;

        TriggerRandomMutation(uid, component);
        component.RadsUntilRandomMutation = _random.NextFloat(MinRadsUntilMutation, MaxRadsUntilMutation);
    }

    public void FillBaseMutations(EntityUid uid, GeneticsComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Mutations.Clear();
        component.BaseMutationIds.Clear();

        var mutationsToAdd = new List<MutationEntry>();
        var addedForcedCount = 0;

        foreach (var forced in component.ForcedBaseMutations)
        {
            if (!(_random.NextFloat() < forced.Chance))
                continue;

            if (!_proto.TryIndex<GeneticMutationPrototype>(forced.Id, out var proto) ||
                !CanEntityReceiveMutation(uid, proto))
                continue;

            var slot = _shuffle.GetOrAssignSlot(forced.Id);
            if (slot.Block <= 0)
                continue;

            var startsActive = _random.Prob(forced.StartActive);
            var revealed = startsActive ? slot.Sequence : RandomizeSequence(slot.Sequence);

            var entry = CreateMutationEntry(forced.Id, proto, slot.Block, slot.Sequence, revealed, startsActive);

            mutationsToAdd.Add(entry);
            component.BaseMutationIds.Add(forced.Id);

            if (startsActive)
            {
                ApplyMutationComponents(uid, component, proto);
            }

            addedForcedCount++;
        }

        var slotsLeft = Math.Max(0, component.MutationSlots - addedForcedCount);

        for (var i = 0; i < slotsLeft; i++)
        {
            var chosenId = PickRandomAvailableMutation(uid, component);
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
            component.BaseMutationIds.Add(chosenId);
        }

        _random.Shuffle(mutationsToAdd);

        foreach (var entry in mutationsToAdd)
        {
            component.Mutations.Add(entry);
        }
    }

    private string? PickRandomAvailableMutation(EntityUid uid, GeneticsComponent component)
    {
        var candidates = _proto.EnumeratePrototypes<GeneticMutationPrototype>()
            .Where(p => CanEntityReceiveMutation(uid, p, true))
            .Where(p => !component.Mutations.Any(m => m.Id == p.ID))
            .Where(p => !IsConflictingWithExisting(component, p))
            .Where(p => !component.BaseMutationIds.Contains(p.ID))
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

    public void TriggerRandomMutation(EntityUid uid, GeneticsComponent component)
    {
        var chosenId = PickRandomAvailableMutation(uid, component);
        if (chosenId == null)
            return;

        var slot = _shuffle.GetOrAssignSlot(chosenId);
        if (slot.Block <= 0)
            return;

        TryAddMutation(uid, component, chosenId);
        TryActivateMutation(uid, component, chosenId);
    }

    public void RemoveRandomMutation(EntityUid uid, GeneticsComponent component, bool mutadone = false)
    {
        var removable = new List<string>();

        foreach (var entry in component.Mutations)
        {
            if (mutadone)
            {
                var proto = _proto.Index<GeneticMutationPrototype>(entry.Id);
                if (proto.MutadoneResistant)
                    continue;
            }

            if (!component.BaseMutationIds.Contains(entry.Id))
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

        TryRemoveMutation(uid, component, chosenId);
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

    public bool TryAddMutation(EntityUid uid, GeneticsComponent component, string mutationId)
    {
        var slot = _shuffle.GetOrAssignSlot(mutationId);

        if (slot == GeneticBlock.Invalid
        || component.Mutations.Any(m => m.Id == mutationId)
        || !_proto.TryIndex(mutationId, out GeneticMutationPrototype? proto))
            return false;

        if (!CanEntityReceiveMutation(uid, proto, false))
            return false;

        if (IsConflictingWithExisting(component, proto))
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

        component.Mutations.Add(entry);

        if (!component.BaseMutationIds.Contains(mutationId))
        {
            ModifyInstability(uid, component, proto.Instability);
        }

        return true;
    }

    private bool TryRemoveMutation(EntityUid uid, GeneticsComponent component, string mutationId)
    {
        var entry = component.Mutations.Find(m => m.Id == mutationId);
        if (entry == null)
            return false;

        var isBase = component.BaseMutationIds.Contains(mutationId);

        if (entry.Enabled)
        {
            if (!TryDeactivateMutation(uid, component, mutationId))
                return false;
        }

        if (isBase)
        {
            return true;
        }

        if (_proto.TryIndex<GeneticMutationPrototype>(mutationId, out var proto))
        {
            ModifyInstability(uid, component, -proto.Instability);
        }

        component.Mutations.Remove(entry);

        return true;
    }

    public bool TryActivateMutation(EntityUid uid, GeneticsComponent component, string mutationId)
    {
        var index = component.Mutations.FindIndex(m => m.Id == mutationId);
        if (index == -1)
            return false;

        var entry = component.Mutations[index];
        if (entry.Enabled)
            return false;

        if (!_proto.TryIndex(mutationId, out GeneticMutationPrototype? proto))
            return false;

        if (!CanEntityReceiveMutation(uid, proto, false))
            return false;

        foreach (var conflictId in proto.Conflicts)
        {
            if (component.Mutations.Any(m => m.Id == conflictId && m.Enabled))
                return false;
        }

        component.Mutations[index] = entry with
        {
            Enabled = true,
            RevealedSequence = entry.OriginalSequence
        };

        ApplyMutationComponents(uid, component, proto);

        var popMsg = !string.IsNullOrWhiteSpace(proto.PopupText)
            ? Loc.GetString(proto.PopupText)
            : Loc.GetString("genetics-mutation-activated");

        _popup.PopupEntity(popMsg, uid, uid);

        return true;
    }

    public bool TryDeactivateMutation(EntityUid uid, GeneticsComponent component, string mutationId)
    {
        for (var i = 0; i < component.Mutations.Count; i++)
        {
            if (component.Mutations[i].Id != mutationId)
                continue;

            if (!component.Mutations[i].Enabled)
                return false;

            if (!_proto.TryIndex<GeneticMutationPrototype>(mutationId, out var proto))
                return false;

            component.Mutations[i] = component.Mutations[i] with { Enabled = false };
            RemoveMutationComponents(uid, proto);
            return true;
        }

        return false;
    }

    private void ApplyMutationComponents(EntityUid uid, GeneticsComponent component, GeneticMutationPrototype proto)
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

    private void ModifyInstability(EntityUid uid, GeneticsComponent component, int delta)
    {
        if (delta == 0)
            return;

        var old = component.GeneticInstability;
        component.GeneticInstability += delta;
        var newVal = component.GeneticInstability;

        if (old <= InstabilityMutationThreshold && newVal > InstabilityMutationThreshold)
        {
            RemComp<PendingInstabilityMutationComponent>(uid);

            var pending = AddComp<PendingInstabilityMutationComponent>(uid);
            pending.MutationId = string.Empty;

            var durationSeconds = _random.Next(MinInstabilityTimerSeconds, MaxInstabilityTimerSeconds);
            pending.EndTime = _timing.CurTime + TimeSpan.FromSeconds(durationSeconds);
        }

        else if (old >= InstabilityMutationThreshold && newVal < InstabilityMutationThreshold)
        {
            if (RemComp<PendingInstabilityMutationComponent>(uid))
            {
                _popup.PopupEntity(Loc.GetString("genetics-instability-cancelled"), uid, uid);
            }
        }

        if (old <= InstabilityDamageThreshold && newVal > InstabilityDamageThreshold)
        {
            EnsureComp<GeneticsInstabilityDamageComponent>(uid);
        }

        else if (old > InstabilityDamageThreshold && newVal <= InstabilityDamageThreshold)
        {
            RemComp<GeneticsInstabilityDamageComponent>(uid);
        }
    }

    public bool CanEntityReceiveMutation(EntityUid uid, GeneticMutationPrototype proto, bool isRandom = true)
    {
        var protoId = MetaData(uid).EntityPrototype?.ID ?? "Unknown";

        if (proto.StrictEntityWhitelist != null && proto.StrictEntityWhitelist.Count > 0)
        {
            if (!UtopiaHelper.IsPrototypeOrParentInList(protoId, proto.StrictEntityWhitelist))
                return false;
        }

        if (proto.StrictEntityBlacklist != null && proto.StrictEntityBlacklist.Count > 0)
        {
            if (UtopiaHelper.IsPrototypeOrParentInList(protoId, proto.StrictEntityBlacklist))
                return false;
        }

        if (isRandom && proto.Hidden)
            return false;

        if (isRandom)
        {
            if (proto.EntityWhitelist != null && proto.EntityWhitelist.Count > 0)
            {
                if (!UtopiaHelper.IsPrototypeOrParentInList(protoId, proto.EntityWhitelist))
                    return false;
            }

            if (proto.EntityBlacklist != null && proto.EntityBlacklist.Count > 0)
            {
                if (UtopiaHelper.IsPrototypeOrParentInList(protoId, proto.EntityBlacklist))
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

    public void ScrambleDna(EntityUid uid, GeneticsComponent genetics)
    {
        var preservedEntries = new List<MutationEntry>();

        foreach (var entry in genetics.Mutations)
        {
            if (!_proto.TryIndex<GeneticMutationPrototype>(entry.Id, out var proto))
                continue;

            if (proto.ScrambleResistant)
                preservedEntries.Add(entry);
        }

        foreach (var entry in genetics.Mutations.ToList())
        {
            if (!entry.Enabled)
                continue;

            if (!_proto.TryIndex<GeneticMutationPrototype>(entry.Id, out var proto) || !proto.ScrambleResistant)
            {
                TryDeactivateMutation(uid, genetics, entry.Id);
            }
        }

        genetics.Mutations.Clear();
        genetics.BaseMutationIds.Clear();

        genetics.GeneticInstability = 0;

        FillBaseMutations(uid, genetics);

        foreach (var preserved in preservedEntries)
        {
            if (TryAddMutation(uid, genetics, preserved.Id))
            {
                var index = genetics.Mutations.FindIndex(m => m.Id == preserved.Id);
                if (index != -1)
                {
                    var newEntry = genetics.Mutations[index];

                    genetics.Mutations[index] = newEntry with
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
