using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Damage.Components;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationIncreaseStaminaCritThresholdSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationIncreaseStaminaCritThresholdComponent, ComponentAdd>(OnAdd);
        SubscribeLocalEvent<MutationIncreaseStaminaCritThresholdComponent, ComponentRemove>(OnRemove);
    }

    private void OnAdd(Entity<MutationIncreaseStaminaCritThresholdComponent> ent, ref ComponentAdd args)
    {
        if (TryComp<StaminaComponent>(ent, out var stamina))
        {
            stamina.CritThreshold += ent.Comp.ThresholdBonus;
            Dirty(ent.Owner, stamina);
        }
    }

    private void OnRemove(Entity<MutationIncreaseStaminaCritThresholdComponent> ent, ref ComponentRemove args)
    {
        if (TryComp<StaminaComponent>(ent, out var stamina))
        {
            stamina.CritThreshold -= ent.Comp.ThresholdBonus;
            stamina.CritThreshold = Math.Max(100f, stamina.CritThreshold);
            Dirty(ent.Owner, stamina);
        }
    }
}
