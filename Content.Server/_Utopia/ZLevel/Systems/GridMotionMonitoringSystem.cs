using System.Collections.Generic;
using System.Numerics;
using Content.Server._Utopia.GridSync;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Utopia.GridSync
{
    public sealed class GridSyncSystem : EntitySystem
    {
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private readonly Dictionary<(MapId, string), List<EntityUid>> _groups = new();

        public override void Update(float frameTime)
        {
            _groups.Clear();

            var query = EntityQueryEnumerator<
                GridSyncGroupComponent,
                PhysicsComponent,
                TransformComponent>();

            while (query.MoveNext(out var uid, out var group, out var physics, out var xform))
            {
                if (group == null || physics == null || xform == null)
                    continue;

                if (xform.GridUid == null)
                    continue;

                var key = (xform.MapID, group.GroupId);

                if (!_groups.TryGetValue(key, out var list))
                {
                    list = new List<EntityUid>();
                    _groups[key] = list;
                }

                list.Add(uid);
            }

            foreach (var ((mapId, groupId), grids) in _groups)
            {
                if (grids.Count < 2)
                    continue;

                Logger.Debug($"[GridSync] Group '{groupId}' Map {mapId} Count {grids.Count}");
                ProcessGroup(grids, frameTime);
            }
        }

        private void ProcessGroup(List<EntityUid> grids, float frameTime)
        {
            float totalWeight = 0f;
            Vector2 velocitySum = Vector2.Zero;
            float angularSum = 0f;
            var rotations = new List<Angle>();

            foreach (var uid in grids)
            {
                if (!TryComp(uid, out PhysicsComponent? physics) ||
                    !TryComp(uid, out GridSyncGroupComponent? sync) ||
                    !TryComp(uid, out TransformComponent? xform))
                    continue;

                totalWeight += sync.Weight;
                velocitySum += physics.LinearVelocity * sync.Weight;
                angularSum += physics.AngularVelocity * sync.Weight;
                rotations.Add(xform.LocalRotation);
            }

            if (totalWeight <= 0f)
                return;

            var targetVelocity = velocitySum / totalWeight;
            var targetAngular = angularSum / totalWeight;
            var targetRotation = AngleAveraging.Average(rotations.ToArray());

            foreach (var uid in grids)
            {
                if (!TryComp(uid, out PhysicsComponent? physics) ||
                    !TryComp(uid, out GridSyncGroupComponent? sync) ||
                    !TryComp(uid, out TransformComponent? xform))
                    continue;

                var lerp = sync.LerpStrength * frameTime;

                var newVel = Vector2.Lerp(
                    physics.LinearVelocity,
                    targetVelocity,
                    lerp);

                var newAng = MathHelper.Lerp(
                    physics.AngularVelocity,
                    targetAngular,
                    lerp);

                var newRot = Angle.Lerp(
                    xform.LocalRotation,
                    targetRotation,
                    lerp);

                _physics.SetLinearVelocity(uid, newVel, body: physics);
                _physics.SetAngularVelocity(uid, newAng, body: physics);
                _transform.SetLocalRotation(uid, newRot, xform);
            }
        }
    }
}