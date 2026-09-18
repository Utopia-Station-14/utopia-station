using Content.Shared._Utopia.Supermatter.Components;
using Content.Server.Lightning;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Supermatter.Systems;

/// <summary>
/// Часть кода Суперматерии, которая отвечает за выстрелы молниями.
/// </summary>
public sealed partial class SupermatterSystem
{
    [Dependency] private LightningSystem _lightning = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// Интересно, пригодится ли когда-нибудь метод в публичном доступе??
    /// </summary>
    public void ShootLightning(Entity<SupermatterComponent> sm, float range, float power)
    {
        power *= 100f; // TODO: Это тотальное говно блять, придумать чё с этим сделать когда-то потом.
        _lightning.ShootRandomLightnings(sm, range, 1, power, GetLightningPrototype(power));
    }

    /// <summary>
    /// Основной метод обработки молний.
    /// </summary>
    private void ProcessLightning(Entity<SupermatterComponent> sm)
    {
        if (_timing.CurTime < sm.Comp.NextLightningTime)
            return;

        sm.Comp.NextLightningTime = _timing.CurTime + GetLightningCooldown(sm);

        // нудно получаем статы
        var energy = sm.Comp.InternalEnergy;
        var count = GetLightningCount(sm, energy);
        var power = GetLightningPower(energy, count);
        var range = GetLightningRange(sm, power, count);

        var uid = sm.Owner;

        for (var i = 0; i < count; i++)
        {
            // Небольшая механика задержки между молниями, если рандом позволяет.
            if (!_random.Prob(DelayedLightningChance))
                ShootLightning(sm, range, power);

            else
            {
                var delay = TimeSpan.FromSeconds(_random.NextFloat(MinDelaySeconds, MaxDelaySeconds));

                Timer.Spawn(delay, () =>
                {
                    if (Deleted(uid) || !TryComp<SupermatterComponent>(uid, out var smComp))
                        return;

                    ShootLightning((uid, smComp), range, power);
                    ProcessLightningEnergy(sm, power); // Не забываем забрать энергию за выстрел.
                });
            }
        }
    }

    /// <summary>
    /// Метод получения кол-ва молний.
    /// </summary>
    private int GetLightningCount(Entity<SupermatterComponent> sm, float energy)
    {
        var count = energy switch
        {
        _ when energy < LowEnergy => 1,
        _ when energy < ToMuchEnergy => _random.Next(1, 4),
        _ => _random.Next(1, 6)
        };

        return count + sm.Comp.LightningCountModifier;
    }

    /// <summary>
    /// Метод получения дальности выстрела молний.
    /// </summary>
    private float GetLightningRange(Entity<SupermatterComponent> sm, float power, int count)
    {
        var range = Math.Clamp(power / 1000f - count, MinLightningRange, MaxLightningRange);
        return range + sm.Comp.LightningRangeModifier;
    }

    /// <summary>
    /// Метод получения времени к следуйщему залпу молний.
    /// </summary>
    private TimeSpan GetLightningCooldown(Entity<SupermatterComponent> sm)
    {
        var cooldown = _random.NextFloat(MinCooldownSeconds, MaxCooldownSeconds);
        cooldown -= sm.Comp.LightningCooldownModifier;
        return TimeSpan.FromSeconds(cooldown);
    }

    /// <summary>
    /// Метод получения энергии передаваемой молнией.
    /// </summary>
    private static float GetLightningPower(float energy, int count)
        => count > 0 ? (energy * 0.8f / count) : 0f; // Вся энергия разделяется между количеством молний.

    private EntProtoId GetLightningPrototype(float power)
    {
        var proto = power switch
        {
            _ when power > ToMuchEnergy => "SupermatterSuperLightning",
            _ when power > DangerAmmountEnergy => "SupermatterChargedLightning",
            _ => "SupermatterLightning"
        };
        return proto;
    }
}
