using Content.Server._Utopia.Genetics.Components;
using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class GeneticsInstabilityDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    private const int InstabilityThreshold = 150;
    private const float DamagePerTick = 1f;
    private const float TickInterval = 2f;
    private const string DamageType = "Cellular";

    private float _accumulator = 0f;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;

        _accumulator -= TickInterval;

        var query = EntityQueryEnumerator<GeneticsComponent, DamageableComponent, GeneticsInstabilityDamageComponent>();
        while (query.MoveNext(out var uid, out var genetics, out var _, out _))
        {
            if (genetics.GeneticInstability <= InstabilityThreshold)
                continue;

            var damage = new DamageSpecifier(ProtoMan.Index<DamageTypePrototype>(DamageType), DamagePerTick);
            _damageable.TryChangeDamage(uid, damage, true);
        }
    }
}
