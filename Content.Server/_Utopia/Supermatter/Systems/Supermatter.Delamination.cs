using Content.Shared._Utopia.Supermatter.Components;
using Content.Shared.Explosion.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private SharedExplosionSystem _explosionSystem = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    private EntProtoId SingularityPrototype = "Singularity";
    private EntProtoId TeslaPrototype = "TeslaEnergyBall";
    private EntProtoId SupermatterKudzu = "SupermatterKudzu";
    private const string SupermatterStabilizer= "SupermatterHzKakNazvat";


    private SoundSpecifier CascadeMusic = new SoundPathSpecifier("/Audio/_Utopia/Supermatter/thecascade.ogg");
    private static readonly ProtoId<DamageTypePrototype> RadiationDamageType = "Radiation";
    private TimeSpan CountdownTimer = TimeSpan.FromSeconds(30);
    private const float MusicRadius = 150f;
    private float TimerModificator = 1f;


    public void SwitchDelamination(Entity<SupermatterComponent> sm)
    {
        sm.Comp.Delamination = !sm.Comp.Delamination;

        if (sm.Comp.CascadeModifier >= 1)
            ProcessCascade(sm);

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

            if (sm.Comp.Integrity >= 1f && sm.Comp.CascadeModifier < 1f)
            {
                CancelDelamination(sm);
                return;
            }

            if (_timing.CurTime >= sm.Comp.DelaminationEndTime)
            {
                ExecuteDelamination(sm);
                return;
            }

            if (_timing.CurTime >= CountdownTimer)
            {
                var remainingSeconds = sm.Comp.DelaminationEndTime.TotalSeconds - _timing.CurTime.TotalSeconds;
                var seconds = (int)Math.Ceiling(remainingSeconds);

                if (seconds > 0)
                {
                    HandleCountdown(sm, seconds);
                    var delay = seconds switch
                    {
                        > 30 => TimeSpan.FromSeconds(10),
                        > 5 => TimeSpan.FromSeconds(5),
                        <= 5 => TimeSpan.FromSeconds(1)
                    };

                    CountdownTimer = _timing.CurTime + delay;
                }
            }

            return;
        }

        if (sm.Comp.Integrity > IntegrityForDelamination)
            return;

        sm.Comp.DelaminationType = GetDelaminationType(sm);
        SwitchDelamination(sm);

        var delayTime = DelaminationTimer / TimerModificator;
        sm.Comp.DelaminationEndTime = _timing.CurTime + delayTime;
        CountdownTimer = _timing.CurTime;
    }

    private void CancelDelamination(Entity<SupermatterComponent> sm)
    {
        ChangeAlertLevel(sm, AlertCodeYellow, false);

        SwitchDelamination(sm);
        TimerModificator += 2f;
    }

    private void ExecuteDelamination(Entity<SupermatterComponent> sm)
    {
        var coords = Transform(sm).Coordinates;

        switch (sm.Comp.DelaminationType)
        {
            case DelaminationType.Cascade:
                SpawnAtPosition(SupermatterKudzu, coords);
                break;
            case DelaminationType.Singularity:
                SpawnAtPosition(SingularityPrototype, coords);
                break;
            case DelaminationType.Tesla:
                SpawnAtPosition(TeslaPrototype, coords);
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

    private void ProcessCascade(Entity<SupermatterComponent> sm)
    {
        PlayAudio(sm, CascadeMusic, true, true);
        SendAnnouncement(sm, Loc.GetString("supermatter-cascade-send"), Color.Orange);

        var stabilizerInside = sm.Comp.StabilizerInside;
        var grid = Transform(sm).GridUid;

        if (grid == null)
            return;

        EntityUid gridId = (EntityUid)grid;
        for (int s = 0; s < stabilizerInside; s++)
            _anomaly.SpawnOnRandomGridLocation(gridId, SupermatterStabilizer);
    }
}
