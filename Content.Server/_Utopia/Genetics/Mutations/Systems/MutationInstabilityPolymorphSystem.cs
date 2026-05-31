using System.Linq;
using Content.Server._Utopia.Genetics.Components;
using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server._Utopia.Genetics.Systems;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._Utopia.Genetics.Prototypes;
using Content.Shared.Buckle.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationInstabilityPolymorphSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly GeneticsSystem _genetics = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationInstabilityPolymorphComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MutationInstabilityPolymorphComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(Entity<MutationInstabilityPolymorphComponent> ent, ref ComponentStartup args)
    {
        if (!HasComp<BuckleComponent>(ent.Owner)) // Fails tests without this.
            return;

        if (!TryComp<GeneticsComponent>(ent.Owner, out var oldGenetics))
        {
            _polymorph.PolymorphEntity(ent.Owner, ent.Comp.PolymorphId);
            return;
        }

        var mutationSnapshot = oldGenetics.Mutations.Select(entry => entry).ToList();
        var enabledMutationIds = new HashSet<string>();
        foreach (var entry in oldGenetics.Mutations)
        {
            if (!entry.Enabled)
                continue;

            if (_proto.TryIndex<GeneticMutationPrototype>(entry.Id, out var proto))
            {
                var addsPolymorphTrigger = proto.Components.Values
                    .Any(c => c.Component is MutationInstabilityPolymorphComponent);

                if (addsPolymorphTrigger)
                    continue;
            }

            enabledMutationIds.Add(entry.Id);
        }

        var instability = oldGenetics.GeneticInstability;
        var baseMutationIds = new HashSet<string>(oldGenetics.BaseMutationIds);
        var newUid = _polymorph.PolymorphEntity(ent.Owner, ent.Comp.PolymorphId);

        if (!newUid.HasValue)
            return;

        var newGenetics = EnsureComp<GeneticsComponent>(newUid.Value);
        newGenetics.Mutations.Clear();

        foreach (var entry in mutationSnapshot)
        {
            newGenetics.Mutations.Add(entry);
        }

        newGenetics.BaseMutationIds = baseMutationIds;
        newGenetics.GeneticInstability = instability;

        foreach (var mutationId in enabledMutationIds)
        {
            _genetics.TryDeactivateMutation((newUid.Value, newGenetics), mutationId);
            _genetics.TryActivateMutation((newUid.Value, newGenetics), mutationId);
        }

        RemCompDeferred<MutationInstabilityPolymorphComponent>(newUid.Value);
        Dirty(newUid.Value, newGenetics);
    }

    private void OnRemove(Entity<MutationInstabilityPolymorphComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<PolymorphedEntityComponent>(ent, out var poly))
            return;

        _polymorph.Revert((ent.Owner, poly));
    }
}
