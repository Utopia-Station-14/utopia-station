using Content.Shared._Utopia.Explosion.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Toxicology.Systems;

/// <summary>
/// Система маяка, который отслеживает параметры взрывов.
/// Чем ближе игроку получается достичь необходимых параметров - тем больше очков он получает.
/// 
/// TODO:
/// Реализовать вывод информации в отдельную консоль.
/// Добавить связывание консоль-маяк мультитулом.
/// Передача очков на сервера РнД.
/// 
/// Отдельно, хочу сделать вывод оповещений в научный канал, если данная функция не отключена в консоли.
/// </summary>
public sealed class ExplosionBeaconSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ExplosionBeaconComponent, ExplosionHitEvent>(OnExplosionHit);
    }

    private void OnExplosionHit(Entity<ExplosionBeaconComponent> beacon, ref ExplosionHitEvent args)
       => Process(beacon, args.Slope, args.TotalIntensity, args.CurrentIntensity);

    private void Process(Entity<ExplosionBeaconComponent> beacon, float slope, float totalIntensity, float currentIntensity)
    {
        var slopePoints = GetPoints(slope, beacon.Comp.TargetSlope);
        var intensityPoints = GetPoints(totalIntensity, beacon.Comp.TargetIntensity);
        var currentPoints = GetPoints(currentIntensity, beacon.Comp.TargetCurrentIntensity);

        var points = (slopePoints + intensityPoints + currentPoints);
        // Кроме получения очков, нужно будет сделать вывод координат взрыва (arg.Epicenter) в консоль/интерком?
        TransferPoints(beacon, points);
    }

    private float GetPoints(float value, float target)
    {
        // Каким раком у тебя 0 вышло ирод.
        if (value <= 0 || target <= 0)
            return 0;

        // Чем ближе у игрока получается попасть к необходимому случайному значению - тем больше очков он получает.
        var points = MathF.Min(value, target) / MathF.Max(value, target);
        points = MathF.Max(points, 0.1f) * 10;

        return (int)points;
    }

    private void TransferPoints(Entity<ExplosionBeaconComponent> beacon, int points)
    {
        // Собственно, у игрока есть 3 попытки, чтобы выполнить текущий таск по взрыву.
        // Если игрок не уложился в 3 попытки, то маяк изменяет необходимые ему параметры взрыва.
        if (beacon.Comp.CurrentAttempt > beacon.Comp.MaxAttempts)
        {
            RandomTargetNumbers();
            beacon.Comp.CurrentAttempt = 0;
            return;
        }
        
        // Не уверен, стоит ли оставлять, но небольшая поблажка игрокам.
        // Если значение очков не меньше минимума, то засчитывать как успешную попытку.
        if (points < beacon.Comp.MinPoints)
            beacon.Comp.CurrentAttempt += 1;

        // Множитель для особо робастных игроков. Если получается закрыть таск с < 1 попытки, то игрок получает небольшую прибавку.
        var multiplier = 1;
        switch (beacon.Comp.CurrentAttempt)
        {
            case 0: 
                multiplier += 2f;
                break;
            case 1: 
                multiplier += 1.5f;
                break;
            default: break;
        }
        points = points * multiplier;
        // Реализовать передачу очков.. куда-нить??
    }

    private void RandomTargetNumbers()
    {
        // Надо потестить параметры взрывов от газов, вынести мин/макс в компонент.
        beacon.Comp.TargetSlope = _random.Next(100, 500); 
        beacon.Comp.TargetIntensity = _random.Next(100, 500);
        beacon.Comp.TargetCurrentIntensity = _random.Next(100, 500);
    }
}