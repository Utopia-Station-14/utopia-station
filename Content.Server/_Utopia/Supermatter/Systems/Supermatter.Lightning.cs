using Content.Shared._Utopia.Supermatter.Components;
using Content.Server.Lightning;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public void ShootLightning(Entity<SupermatterComponent> sm, float range, float power)
    {
        _lightning.ShootRandomLightnings(sm, range, 1, power, GetLightningPrototype(power)); // TODO: Prototypes
        ChangeInternalEnergy(sm, -power);
    }

    private void ProcessLightning(Entity<SupermatterComponent> sm)
    {
        if (_timing.CurTime < sm.Comp.NextLightningTime)
            return;

        sm.Comp.NextLightningTime = _timing.CurTime + GetLightningCooldown();

        var energy = sm.Comp.InternalEnergy;
        var count = GetLightningCount(energy);
        var power = GetLightningPower(energy, count);
        var range = GetLightningRange(power, count);

        var uid = sm.Owner;

        for (var i = 0; i < count; i++)
        {
            if (!_random.Prob(DelayedLightningChance))
            {
                ShootLightning(sm, range, power);
            }
            else
            {
                var delay = TimeSpan.FromSeconds(_random.NextFloat(MinDelaySeconds, MaxDelaySeconds));

                Timer.Spawn(delay, () =>
                {
                    if (Deleted(uid) || !TryComp<SupermatterComponent>(uid, out var smComp))
                        return;

                    ShootLightning((uid, smComp), range, power);
                });
            }
        }
    }

    private int GetLightningCount(float energy) => energy switch
    {
        _ when energy < LowEnergy => 1,
        _ when energy < ToMuchEnergy => _random.Next(1, 4),
        _ => _random.Next(1, 6)
    };

    private static float GetLightningPower(float energy, int count)
        => count > 0 ? (energy * 0.8f / count) : 0f;

    private float GetLightningRange(float power, int count)
        => Math.Clamp(power / 1000f - count, MinLightningRange, MaxLightningRange);

    private TimeSpan GetLightningCooldown()
        => TimeSpan.FromSeconds(_random.NextFloat(MinCooldownSeconds, MaxCooldownSeconds));

    private EntProtoId GetLightningPrototype(float power)
    {
        var proto = power switch
        {
            _ when power > DangerAmmountEnergy => "SupermatterChargedLightning",
            _ when power > ToMuchEnergy => "SupermatterSuperLightning",
            _ => "SupermatterLightning"
        };
        return proto;
    }
}
