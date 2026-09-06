using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationPolymorphSystem : EntitySystem
{
    [Dependency] private PolymorphSystem _polymorph = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationPolymorphComponent, ComponentInit>(OnStartup);
        SubscribeLocalEvent<MutationPolymorphComponent, ComponentRemove>(OnRemove);
    }

    private void OnStartup(Entity<MutationPolymorphComponent> ent, ref ComponentInit args)
    {
        var polymorphId = ent.Comp.PolymorphId;
        _polymorph.PolymorphEntity(ent.Owner, polymorphId);
    }

    private void OnRemove(Entity<MutationPolymorphComponent> ent, ref ComponentRemove args)
    {
        if (!TryComp<PolymorphedEntityComponent>(ent, out var poly))
            return;

        _polymorph.Revert((ent.Owner, poly));
    }
}
