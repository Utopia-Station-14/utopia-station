using System.Linq;
using Content.Server._Utopia.Genetics.Mutations.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server._Utopia.Genetics.Mutations.Systems;

public sealed partial class MutationRegenerationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly string[] RegenTypes =
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Shock",
        "Cold",
        "Caustic"
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationRegenerationComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<MutationRegenerationComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextHeal = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MutationRegenerationComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damageable))
        {
            if (_timing.CurTime < comp.NextHeal)
                continue;

            comp.NextHeal += TimeSpan.FromSeconds(comp.Interval);

            var damage = _damageable.GetAllDamage(uid);

            if (!damage.DamageDict.Any(kvp => RegenTypes.Contains(kvp.Key) && kvp.Value > 0))
                continue;

            var totalToHeal = FixedPoint2.New(comp.HealAmount);
            var typesNeedingHeal = new List<(string Type, FixedPoint2 CurrentDamage)>();

            foreach (var type in RegenTypes)
            {
                if (damage.DamageDict.TryGetValue(type, out var current) && current > 0)
                {
                    typesNeedingHeal.Add((type, current));
                }
            }

            if (typesNeedingHeal.Count == 0)
                continue;

            var healPerType = totalToHeal / typesNeedingHeal.Count;

            var specifier = new DamageSpecifier();

            foreach (var (type, current) in typesNeedingHeal)
            {
                var actualHeal = FixedPoint2.Min(healPerType, current);
                specifier.DamageDict[type] = -actualHeal;
            }

            _damageable.TryChangeDamage(uid, specifier, true);
        }
    }
}
