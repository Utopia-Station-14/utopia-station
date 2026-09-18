using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Explosion.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Player;
using Content.Server._Utopia.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private SharedExplosionSystem _explosionSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private NowPlayingServerSystem _nowPlay = default!;

    private SoundSpecifier CascadeMusic = new SoundPathSpecifier("/Audio/_Utopia/Supermatter/thecascade.ogg");
    private static readonly ProtoId<DamageTypePrototype> RadiationDamageType = "Radiation";
    private EntProtoId SupermatterKudzu = "SupermatterKudzu";
    private const string SupermatterHzKakNazvat = "SupermatterHzKakNazvat";
    private const float MusicRadius = 150f;
    private float TimerModificator = 1f;
    private EntProtoId SingularityPrototype = "Singularity";
    private EntProtoId TeslaPrototype = "TeslaEnergyBall";

    public void SwitchDelamination(Entity<SupermatterComponent> sm)
    {
        sm.Comp.Delamination = !sm.Comp.Delamination;
        ProcessDelaminationAnnouncement(sm);
    }

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
        if (sm.Comp.Delamination)
        {
            if (sm.Comp.Integrity >= 1f)
            {
                CancelDelamination(sm);
                return;
            }

            if (_timing.CurTime >= sm.Comp.DelaminationEndTime)
                ExecuteDelamination(sm);

            return;
        }

        if (sm.Comp.Integrity > IntegrityForDelamination)
            return;

        sm.Comp.DelaminationType = GetDelaminationType(sm);
        SwitchDelamination(sm);

        var delay = DelaminationTimer / TimerModificator;
        sm.Comp.DelaminationEndTime = _timing.CurTime + delay;
    }

    private void CancelDelamination(Entity<SupermatterComponent> sm)
    {
        _alert.SetLevel(sm, AlertCodeYellow, true, true, true, false);
        SwitchDelamination(sm);
        TimerModificator += 2f;
    }

    private void ExecuteDelamination(Entity<SupermatterComponent> sm)
    {
        var coords = Transform(sm).Coordinates;

        switch (sm.Comp.DelaminationType)
        {
            case DelaminationType.Cascade:
                // ProcessCascade(sm);
                break;
            case DelaminationType.Singularity:
                SpawnAtPosition(SingularityPrototype, coords);
                _alert.SetLevel(sm, AlertCodeYellow, true, true, true, false);
                break;
            case DelaminationType.Tesla:
                SpawnAtPosition(TeslaPrototype, coords);
                _alert.SetLevel(sm, AlertCodeYellow, true, true, true, false);
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
            var mapUid = Transform(sm).MapUid;
            var damage = new DamageSpecifier(_prototypeManager.Index(RadiationDamageType), 50);
            var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();

            while (query.MoveNext(out var uid, out var actor, out var transform))
            {
                if (transform.MapUid != mapUid)
                    continue;

                _damageable.TryChangeDamage(uid, damage, ignoreResistances: true);
            }

            _explosionSystem.TriggerExplosive(sm, explosion, true, power, 100f);
        }
    }

    // private void ProcessCascade(Entity<SupermatterComponent> sm)
    // {
    //     if (TryComp(sm, out TransformComponent? xform) && xform.GridUid is { } gridUid)
    //     {
    //         _anomaly.SpawnOnRandomGridLocation(gridUid, SupermatterHzKakNazvat);
    //     }

    //     _nowPlay.NotifyNowPlaying(sm, CascadeMusic, MusicRadius);
    // }
}
