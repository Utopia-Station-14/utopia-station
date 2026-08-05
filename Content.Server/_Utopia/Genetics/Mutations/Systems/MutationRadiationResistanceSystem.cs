using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationRadiationResistanceSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationRadiationResistanceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<MutationRadiationResistanceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<MutationRadiationResistanceComponent> ent, ref ComponentInit args)
    {
        if (!_prototypeManager.TryIndex(ent.Comp.ModifierSetId, out var modifier))
            return;

        var buffComp = EnsureComp<DamageProtectionBuffComponent>(ent.Owner);

        if (!buffComp.Modifiers.ContainsKey(ent.Comp.ModifierSetId))
        {
            buffComp.Modifiers.Add(ent.Comp.ModifierSetId, modifier);
        }
    }

    private void OnShutdown(Entity<MutationRadiationResistanceComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<DamageProtectionBuffComponent>(ent, out var buffComp))
            return;

        buffComp.Modifiers.Remove(ent.Comp.ModifierSetId);

        if (buffComp.Modifiers.Count == 0)
        {
            RemComp<DamageProtectionBuffComponent>(ent.Owner);
        }
    }
}
