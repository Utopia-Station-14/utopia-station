using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared._Utopia.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<MovedByPressureComponent> _movedByPressureQuery = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedAppearanceSystem _spaceWindAppearance = default!;

    private static readonly ProtoId<SoundCollectionPrototype> DefaultSpaceWindSounds = "SpaceWind";
    private static readonly EntProtoId? SpaceWindEffect = "EffectSpaceWind";

    private const float MinPushPressure = 2000f;
    private const float FullWindPressure = 4000f;
    private const float LeakNearDistance = 1.6f;
    private const float LeakFarDistance = 3.5f;
    private const float MaxWindSpeed = 8f;
    private const float KnockdownForceThreshold = 40f;
    private const int DownstreamSpreadTiles = 5;

    private int _spaceWindSoundCooldown;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool SpaceWindDebug { get; set; }

    [ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SpaceWindSound { get; private set; } =
        new SoundCollectionSpecifier(DefaultSpaceWindSounds, AudioParams.Default.WithVariation(0.125f));

    private readonly HashSet<Entity<MovedByPressureComponent>> _activePressures = new(8);

    private void UpdateHighPressure(float frameTime)
    {
        var toRemove = new RemQueue<Entity<MovedByPressureComponent>>();

        foreach (var ent in _activePressures)
        {
            var (uid, comp) = ent;
            MetaDataComponent? metadata = null;

            if (Deleted(uid, metadata))
            {
                toRemove.Add(ent);
                continue;
            }

            if (Paused(uid, metadata))
                continue;

            comp.Accumulator += frameTime;
            if (comp.Accumulator < 2f)
                continue;

            comp.Accumulator = 0f;
            toRemove.Add(ent);

            if (HasComp<MobStateComponent>(uid) && TryComp<PhysicsComponent>(uid, out var body))
                _physics.SetBodyStatus(uid, body, BodyStatus.OnGround);

            if (TryComp<FixturesComponent>(uid, out var fixtures))
            {
                foreach (var (id, fixture) in fixtures.Fixtures)
                {
                    if (comp.TableLayerRemoved.Contains(id))
                        _physics.AddCollisionMask(uid, id, fixture, (int)CollisionGroup.TableLayer, manager: fixtures);
                }
            }
        }

        foreach (var comp in toRemove)
            _activePressures.Remove(comp);
    }

    private void AddMobMovedByPressure(EntityUid uid, MovedByPressureComponent component, PhysicsComponent body)
    {
        if (!TryComp<FixturesComponent>(uid, out var fixtures))
            return;

        _physics.SetBodyStatus(uid, body, BodyStatus.InAir);
        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            if ((fixture.CollisionMask & (int)CollisionGroup.TableLayer) != 0)
            {
                component.TableLayerRemoved.Add(id);
                _physics.RemoveCollisionMask(uid, id, fixture, (int)CollisionGroup.TableLayer, manager: fixtures);
            }
        }

        component.Accumulator = 0f;
        _activePressures.Add((uid, component));
    }

    private void HighPressureMovements(Entity<GridAtmosphereComponent> gridAtmosphere, TileAtmosphere tile)
    {
        if (tile.PressureDifference > 15f && !tile.Hotspot.Valid && _spaceWindSoundCooldown == 0 && SpaceWindSound != null)
        {
            var soundCoordinates = _mapSystem.ToCenterCoordinates(tile.GridIndex, tile.GridIndices);
            var volume = MathHelper.Clamp(tile.PressureDifference / 10f, 10f, 100f);
            _audio.PlayPvs(SpaceWindSound, soundCoordinates, SpaceWindSound.Params.WithVolume(volume));
        }

        if (_spaceWindSoundCooldown++ > 75)
            _spaceWindSoundCooldown = 0;

        var gridWorldRotation = XformSystem.GetWorldRotation(gridAtmosphere);

        if (SpaceWind && MonstermosEqualization && tile.PressureSpecificTarget == null)
        {
            var curTile = tile;
            for (var i = 0; i < 3; i++)
            {
                if (curTile.PressureDirection == AtmosDirection.Invalid ||
                    !curTile.AdjacentBits.IsFlagSet(curTile.PressureDirection))
                    break;

                curTile = curTile.AdjacentTiles[curTile.PressureDirection.ToIndex()]!;
                if (curTile.Space)
                    break;
            }

            if (curTile != tile)
                tile.PressureSpecificTarget = curTile;
        }

        SpawnSpaceWindEffects(tile, gridWorldRotation);
        if (!SpaceWind)
            return;

        var effectivePressure = tile.PressureDifference;
        if (tile.PressureSpecificTarget is { Space: true } breach)
        {
            var diff = breach.GridIndices - tile.GridIndices;
            var distance = new Vector2(diff.X, diff.Y).Length();
            var floor = MathHelper.Lerp(MinPushPressure, FullWindPressure, LeakProximity(distance));
            effectivePressure = MathF.Max(effectivePressure, floor);
        }

        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(tile.GridIndex, tile.GridIndices, _entSet, 0f);

        foreach (var entity in _entSet)
        {
            if (!_physicsQuery.TryGetComponent(entity, out var body) ||
                !_movedByPressureQuery.TryGetComponent(entity, out var pressure) ||
                !pressure.Enabled ||
                _containers.IsEntityInContainer(entity))
                continue;

            var pressureMovements = EnsureComp<MovedByPressureComponent>(entity);
            if (pressure.LastHighPressureMovementAirCycle < gridAtmosphere.Comp.UpdateCounter)
            {
                var targetCoords = tile.PressureSpecificTarget is { Space: true } spaceTarget
                    ? _mapSystem.ToCenterCoordinates(tile.GridIndex, spaceTarget.GridIndices)
                    : EntityCoordinates.Invalid;

                ExperiencePressureDifference(
                    (entity, pressureMovements),
                    gridAtmosphere.Comp.UpdateCounter,
                    effectivePressure,
                    tile.PressureDirection,
                    0f,
                    targetCoords,
                    gridWorldRotation,
                    Transform(entity),
                    body);
            }
        }
    }

    private static float LeakProximity(float distance)
    {
        var t = 1f - Math.Clamp((distance - LeakNearDistance) / (LeakFarDistance - LeakNearDistance), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private void SpawnSpaceWindEffects(TileAtmosphere tile, Angle gridWorldRotation)
    {
        if (SpaceWindEffect is not { } effect)
            return;

        var pd = tile.PressureDifference;
        if (pd <= 30f || tile.PressureDirection == AtmosDirection.Invalid || !_random.Prob(0.35f))
            return;

        var flow = tile.PressureDirection.ToAngle().ToWorldVec();
        if (tile.PressureSpecificTarget != null)
        {
            var diff = tile.PressureSpecificTarget.GridIndices - tile.GridIndices;
            var toTarget = new Vector2(diff.X, diff.Y);

            if (toTarget.LengthSquared() > 1e-4f && toTarget.Length() <= LeakFarDistance)
            {
                var blended = flow + toTarget.Normalized();
                if (blended.LengthSquared() > 1e-4f)
                    flow = blended.Normalized();
            }
        }

        var baseAngle = flow.ToWorldAngle() + gridWorldRotation;
        var strength = Math.Clamp((pd - 30f) / 570f, 0f, 1f);
        var baseAlpha = MathHelper.Lerp(0.12f, 0.8f, strength);
        var count = pd > 400f ? 3 : pd > 150f ? 2 : 1;

        var center = _mapSystem.ToCenterCoordinates(tile.GridIndex, tile.GridIndices);
        for (var i = 0; i < count; i++)
        {
            var offset = new Vector2(_random.NextFloat(-0.4f, 0.4f), _random.NextFloat(-0.4f, 0.4f));
            var effectEnt = Spawn(effect, center.Offset(offset));

            XformSystem.SetWorldRotation(effectEnt, baseAngle + new Angle(_random.NextFloat(-0.15f, 0.15f)));
            var alpha = Math.Clamp(baseAlpha * _random.NextFloat(0.7f, 1f), 0f, 1f);
            _spaceWindAppearance.SetData(effectEnt, SpaceWindVisuals.Alpha, alpha);
        }
    }

    private void ConsiderPressureDifference(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, AtmosDirection differenceDirection, float difference)
    {
        if (tile.Air != null
            && tile.AdjacentBits.IsFlagSet(differenceDirection)
            && tile.AdjacentTiles[differenceDirection.ToIndex()] is { Air: { } receivingAir })
        {
            var gradient = tile.Air.Pressure - receivingAir.Pressure;
            if (gradient > Atmospherics.OneAtmosphere)
                difference = MathF.Max(difference, MinPushPressure + (gradient - Atmospherics.OneAtmosphere) * 50f);
        }

        AddPressureDifference(gridAtmosphere, tile, differenceDirection, difference);

        if (difference < 1f)
            return;

        var current = tile;
        var spread = difference;
        for (var i = 0; i < DownstreamSpreadTiles; i++)
        {
            if (!current.AdjacentBits.IsFlagSet(differenceDirection))
                break;

            var next = current.AdjacentTiles[differenceDirection.ToIndex()];
            if (next == null || next.Space)
                break;

            spread *= 0.85f;
            AddPressureDifference(gridAtmosphere, next, differenceDirection, spread);
            current = next;
        }
    }

    private static void AddPressureDifference(GridAtmosphereComponent gridAtmosphere, TileAtmosphere tile, AtmosDirection differenceDirection, float difference)
    {
        gridAtmosphere.HighPressureDelta.Add(tile);

        if (difference <= tile.PressureDifference)
            return;

        tile.PressureDifference = difference;
        tile.PressureDirection = differenceDirection;
    }

    public void ExperiencePressureDifference(Entity<MovedByPressureComponent> ent, int cycle, float pressureDifference, AtmosDirection direction,
        float pressureResistanceProbDelta, EntityCoordinates throwTarget, Angle gridWorldRotation, TransformComponent? xform = null, PhysicsComponent? physics = null)
    {
        var (uid, component) = ent;
        if (!Resolve(uid, ref physics, false) || !Resolve(uid, ref xform))
            return;

        var maxForce = MathF.Sqrt(pressureDifference) * 2.25f;
        var moveProb = component.PressureResistance > 0
            ? MathF.Abs((pressureDifference / component.PressureResistance * MovedByPressureComponent.ProbabilityBasePercent) -
                        MovedByPressureComponent.ProbabilityOffset)
            : 100f;

        var canMoveDynamic = moveProb > MovedByPressureComponent.ProbabilityOffset
                             && _random.Prob(MathF.Min(moveProb / 100f, 1f))
                             && !float.IsPositiveInfinity(component.MoveResist)
                             && physics.BodyType != BodyType.Static
                             && maxForce >= component.MoveResist * MovedByPressureComponent.MoveForcePushRatio;

        var canMoveStatic = physics.BodyType == BodyType.Static
                            && maxForce >= component.MoveResist * MovedByPressureComponent.MoveForceForcePushRatio;

        if (!canMoveDynamic && !canMoveStatic)
        {
            if (SpaceWindDebug)
                Log.Info($"SpaceWind {ToPrettyString(uid)}: NOT moved, pd={pressureDifference:F1} maxForce={maxForce:F1} moveProb={moveProb:F1} moveResist={component.MoveResist} dir={direction}");
            return;
        }

        var dir = (direction.ToAngle() + gridWorldRotation).ToWorldVec();
        var proximity = Math.Clamp(pressureDifference / FullWindPressure, 0.5f, 1f);
        var hasTarget = throwTarget != EntityCoordinates.Invalid;
        if (hasTarget)
        {
            var toTarget = XformSystem.ToMapCoordinates(throwTarget).Position - XformSystem.GetWorldPosition(xform);
            proximity = LeakProximity(toTarget.Length());

            if (toTarget.LengthSquared() > 1e-4f)
            {
                var blended = toTarget.Normalized() * proximity + dir;
                if (blended.LengthSquared() > 1e-4f)
                    dir = blended.Normalized();
            }
        }

        if (HasComp<MobStateComponent>(uid))
        {
            AddMobMovedByPressure(uid, component, physics);
            if (maxForce >= KnockdownForceThreshold && proximity >= 0.3f)
            {
                var duration = TimeSpan.FromSeconds(MathF.Min(maxForce * 0.02f, 3f));
                _stun.TryKnockdown(uid, duration, refresh: false);
            }
        }

        var proximityScale = 0.25f + 0.75f * proximity;
        var speedCap = MathF.Min(MaxWindSpeed * (hasTarget ? proximityScale : MathF.Min(proximityScale, 0.75f)), SpaceWindMaxVelocity);
        var speedPerForce = 0.06f * (1f + proximity);

        var targetSpeed = MathF.Min(
            maxForce * speedPerForce * Math.Clamp(moveProb / 100f, 0f, 1f),
            speedCap);

        var smoothing = MathHelper.Lerp(0.25f, 0.4f, proximity);
        var maxDeltaV = MaxWindSpeed * (0.1f + 0.4f * proximity);

        var currentSpeed = Vector2.Dot(physics.LinearVelocity, dir);
        var deltaV = MathHelper.Clamp((targetSpeed - currentSpeed) * smoothing, 0f, maxDeltaV);

        if (SpaceWindDebug)
            Log.Info($"SpaceWind {ToPrettyString(uid)}: pd={pressureDifference:F1} maxForce={maxForce:F1} dir={direction} prox={proximity:F2} target={targetSpeed:F2} cur={currentSpeed:F2} dV={deltaV:F2}");

        if (deltaV > 0f)
            _physics.ApplyLinearImpulse(uid, dir * (deltaV * physics.Mass), body: physics);

        component.LastHighPressureMovementAirCycle = cycle;
    }
}
