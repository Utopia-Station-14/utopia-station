using System.Numerics;
using JetBrains.Annotations;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Utopia.CCVar;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] protected TurfSystem Turf = default!;

    private void UpdateMovement(EntityUid uid, CEZPhysicsComponent zPhys, TransformComponent xform,
        PhysicsComponent physics, float frameTime)
    {
        if (zPhys.IgnoreHighGround)
            return;

        var oldVelocity = zPhys.Velocity;
        var oldHeight = zPhys.LocalPosition;

        var hasGravity = IsGravityEnabled(uid);

        if (hasGravity && physics.BodyStatus == BodyStatus.OnGround)
        {
            var velocityEv = new CEGetZVelocityEvent((uid, zPhys));
            RaiseLocalEvent(uid, velocityEv);

            zPhys.Velocity += velocityEv.VelocityDelta * frameTime;
        }

        zPhys.LocalPosition += zPhys.Velocity * frameTime;

        var hasGround = TryGetGround(uid, zPhys, out var groundHeight);
        zPhys.CurrentGroundHeight = groundHeight;

        UpdateGrounded(uid, zPhys, hasGround, frameTime, out var landed);
        HandleLevelChange(uid, zPhys);

        if (landed)
            HandleFalling(uid, zPhys);

        if (Math.Abs(zPhys.Velocity) > ZVelocityLimit)
            zPhys.Velocity = Math.Sign(zPhys.Velocity) * ZVelocityLimit;

        if (Math.Abs(oldVelocity - zPhys.Velocity) > 0.001f)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.Velocity));

        if (Math.Abs(oldHeight - zPhys.LocalPosition) > 0.001f)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.LocalPosition));
    }

    private bool TryGetGround(EntityUid uid, CEZPhysicsComponent zPhys, out float ground)
    {
        ground = 0;

        var xform = Transform(uid);

        if (!GridQuery.HasComp(xform.GridUid) || IsSpaceTile(uid))
            return false;

        ground = ComputeGroundHeightInternal((uid, zPhys), out _);
        return true;
    }

    private void UpdateGrounded(EntityUid uid, CEZPhysicsComponent zPhys, bool hasGround, float frameTime, out bool landed)
    {
        landed = false;

        if (!hasGround)
        {
            if (zPhys.IsGrounded)
            {
                zPhys.IsGrounded = false;
                DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.IsGrounded));
            }

            HandleLevelChange(uid, zPhys);
        }

        var distanceToGround = zPhys.LocalPosition - zPhys.CurrentGroundHeight;
        var currentlyGrounded = distanceToGround <= MaxStepHeight || zPhys.CurrentStickyGround;

        if (currentlyGrounded)
        {
            zPhys.LocalPosition -= distanceToGround;
        }

        if (currentlyGrounded == zPhys.IsGrounded)
            return;

        landed = !zPhys.IsGrounded && currentlyGrounded;

        zPhys.IsGrounded = currentlyGrounded;
        DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.IsGrounded));
    }

    private void HandleFalling(EntityUid uid, CEZPhysicsComponent zPhys)
    {
        if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
        {
            _queuedLandings.Add(uid, -zPhys.Velocity);
        }

        SetZVelocity((uid, zPhys), -zPhys.Velocity * zPhys.Bounciness);
    }

    [PublicAPI]
    public bool IsSpaceTile(EntityUid uid)
    {
        var xform = Transform(uid);

        if (!Turf.TryGetTileRef(xform.Coordinates, out var tileRef) || tileRef == null)
            return false;

        var tile = tileRef.Value.Tile;
        return tile.IsEmpty;
    }

    private void HandleLevelChange(EntityUid uid, CEZPhysicsComponent zPhys)
    {
        var xform = Transform(uid);

        if (zPhys.LocalPosition < 0)
        {
            if (_config.GetCVar(UCCVars.FallToBackroomsEnabled))
                return;

            if (!TryMoveDown(uid, xform))
            {
                SetZVelocity((uid, zPhys), 0);
                zPhys.LocalPosition = zPhys.CurrentGroundHeight;
                return;
            }

            if (zPhys.CurrentStickyGround)
                return;

            if (!IsGravityEnabled(uid))
            {
                SetZVelocity((uid, zPhys), 0);
            }

            RaiseLocalEvent(uid, new CEZLevelFallMapEvent());
            return;
        }
        else if (zPhys.LocalPosition >= 1)
        {
            if (HasTileAbove(uid))
            {
                if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                {
                    _queuedLandings.Add(uid, zPhys.Velocity);
                }

                zPhys.LocalPosition = 1;
                SetZVelocity((uid, zPhys), -zPhys.Velocity * zPhys.Bounciness);
            }
            else
            {
                if (TryMoveUp(uid))
                {
                    zPhys.LocalPosition -= 1;
                }
            }
        }
    }

    private bool TryMoveDown(EntityUid uid, TransformComponent xform)
    {
        if (!_zMapQuery.TryComp(xform.MapUid, out var zMap))
            return false;

        if (!TryMapOffset((xform.MapUid.Value, zMap), -1, out var below) || below == null)
            return false;

        if (!TryMoveDownOrChasm(uid))
            return false;

        return true;
    }

    private bool IsGravityEnabled(EntityUid uid)
    {
        if (TryComp<GravityAffectedComponent>(uid, out var grav))
            return !_gravity.IsWeightless((uid, grav));

        return true;
    }

    private float ComputeGroundHeightInternal(Entity<CEZPhysicsComponent?> target, out bool stickyGround, int maxFloors = 1)
    {
        stickyGround = false;

        if (!Resolve(target, ref target.Comp, false))
            return 0;

        var xform = Transform(target);

        if (!_zMapQuery.TryComp(xform.MapUid, out var zMapComp))
            return 0;

        if (!GridQuery.TryComp(xform.MapUid, out var mapGrid))
            return 0;

        var worldPosI = _transform.GetGridOrMapTilePosition(target);
        var worldPos = _transform.GetWorldPosition(target);

        Entity<CEZLevelMapComponent> checkingMap = (xform.MapUid.Value, zMapComp);
        var checkingGrid = mapGrid;

        for (var floor = 0; floor <= maxFloors; floor++)
        {
            if (floor != 0)
            {
                if (!TryMapOffset((checkingMap.Owner, checkingMap.Comp), -floor, out var tempMap))
                    continue;

                if (!GridQuery.TryComp(tempMap, out var tempGrid))
                    continue;

                checkingMap = tempMap.Value;
                checkingGrid = tempGrid;
            }

            var query = MapSys.GetAnchoredEntitiesEnumerator(checkingMap, checkingGrid, worldPosI);

            while (query.MoveNext(out var ent))
            {
                if (!_highgroundQuery.TryComp(ent, out var heightComp))
                    continue;

                var uid = ent.Value;

                var fix = _fix.GetFixtureOrNull(uid, heightComp.FixtureId);

                if (fix == null || fix.Shape is not PolygonShape shape)
                    continue;

                var transform = new Transform(0f);
                var bottom = shape.ComputeAABB(transform, 0).Bottom;
                var top = shape.ComputeAABB(transform, 0).Top;
                var length = Math.Abs(top - bottom);

                var (pos, rot) = _transform.GetWorldPositionRotation(uid);
                var bottomPos = rot.RotateVec(new Vector2(0, bottom)) + pos;

                var curve = heightComp.HeightCurve;
                if (curve.Count == 0)
                    continue;

                if (curve.Count == 1)
                    return -floor + curve[0];

                var worldDir = rot.RotateVec(new Vector2(0, length));
                var lenWorld = worldDir.Length();
                if (lenWorld == 0)
                    continue;

                stickyGround = heightComp.Stick;

                var relPos = worldPos - bottomPos;
                var t = Vector2.Dot(relPos, worldDir) / (lenWorld * lenWorld);
                t = Math.Clamp(t, 0f, 1f);
                t = 1f - t;

                var index = t * (curve.Count - 1);
                var lower = (int)Math.Floor(index);
                var upper = Math.Min(lower + 1, curve.Count - 1);
                var frac = index - lower;

                var y = curve[lower] * (1 - frac) + curve[upper] * frac;

                return -floor + y;
            }

            if (MapSys.TryGetTileRef(checkingMap, checkingGrid, worldPosI, out var tileRef) &&
                !tileRef.Tile.IsEmpty)
            {
                return -floor;
            }
        }

        return -maxFloors;
    }
}
