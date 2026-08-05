using Content.Server.Administration.Logs;
using Content.Server.Stack;
using Content.Shared_Utopia.Effects;
using Content.Shared.Database;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Teleportation;

public sealed class TeleportSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomTeleportOnUseComponent, UseInHandEvent>(OnUseInHand);

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
    }

    private void OnUseInHand(EntityUid uid, RandomTeleportOnUseComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<RandomTeleportComponent>(uid, out var teleport))
            return;

        RandomTeleport(args.User, teleport);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(args.User):actor} teleported with {ToPrettyString(uid)}");

        args.Handled = true;

        if (component.ConsumeOnUse)
        {
            if (TryComp<StackComponent>(uid, out var stack))
            {
                _stack.SetCount((uid, stack), stack.Count - 1);
                return;
            }

            QueueDel(uid);
        }
    }

    public void RandomTeleport(EntityUid uid, RandomTeleportComponent component)
    {
        RandomTeleport(uid, component.TeleportRadius, component.ArrivalSound, component.TeleportAttempts);
    }

    public void RandomTeleport(EntityUid uid, float radius, SoundSpecifier audio, int attempts)
    {
        if (TryComp<PullableComponent>(uid, out var pull) && _pullingSystem.IsPulled(uid, pull))
        {
            _pullingSystem.TryStopPull(uid, pull);
        }

        var xform = Transform(uid);
        var entityCoords = _xform.ToMapCoordinates(xform.Coordinates);
        var targetCoords = new MapCoordinates();

        for (var i = 0; i < Math.Max(attempts, 1); i++)
        {
            var distance = radius * MathF.Sqrt(_random.NextFloat());
            targetCoords = entityCoords.Offset(_random.NextAngle().ToVec() * distance);

            if (!_mapManager.TryFindGridAt(targetCoords, out var gridUid, out var grid))
                continue;

            var valid = true;
            foreach (var entity in _map.GetAnchoredEntities((gridUid, grid), targetCoords))
            {
                if (!_physicsQuery.TryGetComponent(entity, out var body))
                    continue;

                if (body.BodyType != BodyType.Static || !body.Hard
                || (body.CollisionLayer & (int)CollisionGroup.Impassable) == 0)
                    continue;

                valid = false;
                break;
            }

            if (valid)
                break;
        }

        _audio.PlayPvs(audio, uid);
        _xform.SetWorldPosition(uid, targetCoords.Position);
        _audio.PlayPvs(audio, uid);
    }
}
