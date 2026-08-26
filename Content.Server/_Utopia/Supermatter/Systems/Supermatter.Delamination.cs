using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Explosion.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Player;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [DataField] private SharedExplosionSystem _explosionSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private EntProtoId SingularityPrototype = "Singularity";
    private EntProtoId TeslaPrototype = "TeslaEnergyBall";

    public DelaminationType GetDelaminationType(Entity<SupermatterComponent> sm)
    {
        if (sm.Comp.AtmosGas.TotalMoles > ToMuchGas)
            return DelaminationType.Singularity;

        if (sm.Comp.TotalEnergy >= ToMuchEnergy)
            return DelaminationType.Tesla;

        return DelaminationType.Explosion;
    }

    private void ProcessDelamination(Entity<SupermatterComponent> sm)
    {
        if (sm.Comp.Integrity > IntegrityForDelamination)
            return;

        var coords = Transform(sm).Coordinates;
        switch (GetDelaminationType(sm))
        {
            case DelaminationType.Cascade:
                ProcessCascade(sm);
                break;
            case DelaminationType.Singularity:
                SpawnCatastrophe(sm, coords, SingularityPrototype);
                break;
            case DelaminationType.Tesla:
                SpawnCatastrophe(sm, coords, TeslaPrototype);
                break;
            default:
                ProcessExplosion(sm, coords);
                break;
        }
    }

    private void ProcessExplosion(Entity<SupermatterComponent> sm, EntityCoordinates coords)
    {
        var power = sm.Comp.TotalEnergy;
        if (TryComp<ExplosiveComponent>(sm, out var explosion))
        {
            ProcessPlayers(sm);
            _explosionSystem.TriggerExplosive(sm, explosion, true, power, 100f);
        }

    }

    private void SpawnCatastrophe(Entity<SupermatterComponent> sm, EntityCoordinates coords, EntProtoId entity)
    {
        EntityManager.SpawnEntity(entity, coords);
    }

    private void ProcessCascade(Entity<SupermatterComponent> sm)
    { }

    private void ProcessPlayers(Entity<SupermatterComponent> sm)
    {
        var mapUid = Transform(sm).MapUid;

        var radType = _prototypeManager.Index<DamageTypePrototype>("Radiation");
        var damageToDeal = new DamageSpecifier(radType, 50);

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var actor, out var transform))
        {
            if (transform.MapUid != mapUid)
                continue;

            _damageable.TryChangeDamage(uid, damageToDeal, ignoreResistances: false);
        }
    }
}
