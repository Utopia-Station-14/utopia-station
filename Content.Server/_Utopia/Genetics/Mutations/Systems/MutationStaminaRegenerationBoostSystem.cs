using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Server.Damage.Systems;
using Content.Shared.Damage.Components;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed class MutationStaminaRegenerationBoostSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _staminaSystem = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MutationStaminaRegenerationBoostComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var boost, out var stamina))
        {
            if (stamina.ActiveDrains.Count == 0)
            {
                _staminaSystem.TakeStaminaDamage(uid, -boost.RegenBonus * frameTime, stamina, visual: false);
            }
        }
    }
}
