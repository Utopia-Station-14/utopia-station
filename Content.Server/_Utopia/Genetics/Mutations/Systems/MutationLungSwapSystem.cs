using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Containers;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationLungSwapSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private const string HiddenStorageContainerId = "mutation_hidden_lung_storage";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationLungSwapComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MutationLungSwapComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<MutationLungSwapComponent> ent, ref ComponentStartup args)
    {
        if (!TryGetLungOrgan(ent.Owner, out var originalLungNullable)
        || originalLungNullable is not { } originalLung)
        {
            RemComp<MutationLungSwapComponent>(ent.Owner);
            return;
        }

        if (!TryGetLungSlot(ent.Owner, out var lungSlot) || lungSlot is null)
        {
            RemComp<MutationLungSwapComponent>(ent.Owner);
            return;
        }

        ent.Comp.OriginalLung = originalLung;

        _container.Remove(originalLung, lungSlot);

        var hiddenContainer = _container.EnsureContainer<ContainerSlot>(ent.Owner, HiddenStorageContainerId);
        _container.Insert(originalLung, hiddenContainer);

        var newLung = Spawn(ent.Comp.NewLungPrototype, Transform(ent.Owner).Coordinates);
        ent.Comp.SwappedLung = newLung;

        _container.Insert(newLung, lungSlot);
    }

    private void OnShutdown(Entity<MutationLungSwapComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.OriginalLung is not { Valid: true }
        || ent.Comp.SwappedLung is not { Valid: true })
            return;

        if (!TryGetLungSlot(ent.Owner, out var lungSlot) || lungSlot is null)
            return;

        if (lungSlot.ContainedEntity is { } current)
        {
            _container.Remove(current, lungSlot);
            Del(current);
        }

        if (_container.TryGetContainer(ent.Owner, HiddenStorageContainerId, out var baseHiddenContainer)
        && baseHiddenContainer is ContainerSlot hiddenContainer
        && hiddenContainer.ContainedEntity is { } storedLung)
        {
            _container.Remove(storedLung, hiddenContainer);
            _container.Insert(storedLung, lungSlot);
        }

        ent.Comp.OriginalLung = null;
        ent.Comp.SwappedLung = null;

        if (_container.TryGetContainer(ent.Owner, HiddenStorageContainerId, out var cleanupBase)
        && cleanupBase is ContainerSlot cleanupSlot
        && cleanupSlot.ContainedEntity is null)
        {
            _container.ShutdownContainer(cleanupSlot);
        }
    }

    private bool TryGetLungOrgan(EntityUid body, out EntityUid? lung)
    {
        lung = null;

        foreach (var (organUid, _) in _body.GetBodyOrgans(body))
        {
            if (HasComp<LungComponent>(organUid))
            {
                lung = organUid;
                return true;
            }
        }

        return false;
    }

    private bool TryGetLungSlot(EntityUid body, out ContainerSlot? slot)
    {
        slot = null;

        foreach (var (partId, _) in _body.GetBodyChildren(body))
        {
            if (_container.TryGetContainer(partId, "body_organ_slot_lungs", out var container)
            && container is ContainerSlot organSlot)
            {
                slot = organSlot;
                return true;
            }
        }

        return false;
    }
}
