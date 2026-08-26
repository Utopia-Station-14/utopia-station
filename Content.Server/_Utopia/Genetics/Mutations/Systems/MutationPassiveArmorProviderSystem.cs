using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationPassiveArmorProviderSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationPassiveArmorProviderComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<MutationPassiveArmorProviderComponent> ent, ref DamageModifyEvent args)
    {
        if (!ProtoMan.TryIndex(ent.Comp.ModifierSetId, out var proto))
            return;

        DamageModifierSet modifiers = proto;
        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
    }
}
