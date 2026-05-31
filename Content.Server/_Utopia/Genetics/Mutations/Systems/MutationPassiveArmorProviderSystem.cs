using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationPassiveArmorProviderSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationPassiveArmorProviderComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<MutationPassiveArmorProviderComponent> ent, ref DamageModifyEvent args)
    {
        if (!_proto.TryIndex(ent.Comp.ModifierSetId, out var proto))
            return;

        DamageModifierSet modifiers = proto;
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
    }
}
