using System.Numerics;
using JetBrains.Annotations;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Utopia.CCVar;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] protected readonly TurfSystem Turf = default!;

    public const string ZTileID = "UtopiaSpace";
    private const float NoGround = float.NegativeInfinity;

    private void UpdateMovement(EntityUid uid, CEZPhysicsComponent zPhys, TransformComponent xform,
        PhysicsComponent physics, float frameTime)
    {
        var oldVelocity = zPhys.Velocity;
        var oldHeight = zPhys.LocalPosition;

        var hasGravity = IsGravityEnabled(uid);

        if (hasGravity)
        {
            var velocityEv = new CEGetZVelocityEvent((uid, zPhys));
            RaiseLocalEvent(uid, velocityEv);

            zPhys.Velocity += velocityEv.VelocityDelta * frameTime;
        }

        zPhys.LocalPosition += zPhys.Velocity * frameTime;
        zPhys.Velocity = Math.Clamp(zPhys.Velocity, -ZVelocityLimit, ZVelocityLimit);

        var hasGround = TryGetGround(uid, zPhys, out var groundHeight);

        if (groundHeight == NoGround)
        {
            if (zPhys.IsGrounded)
            {
                zPhys.IsGrounded = false;
                DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.IsGrounded));
            }

            if (zPhys.Velocity >= 0)
                zPhys.Velocity = -1f;

            HandleLevelChange(uid, zPhys, false);

            if (Math.Abs(oldVelocity - zPhys.Velocity) > 0.01f)
                DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.Velocity));

            if (Math.Abs(oldHeight - zPhys.LocalPosition) > 0.01f)
                DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.LocalPosition));

            return;
        }

        UpdateGrounded(uid, zPhys, hasGround, groundHeight, frameTime, out var landed);
        HandleLevelChange(uid, zPhys, hasGround);

        if (landed)
            HandleFalling(uid, zPhys);

        if (Math.Abs(oldVelocity - zPhys.Velocity) > 0.01f)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.Velocity));

        if (Math.Abs(oldHeight - zPhys.LocalPosition) > 0.01f)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.LocalPosition));
    }

    private bool TryGetGround(EntityUid uid, CEZPhysicsComponent zPhys, out float ground)
    {
        ground = 0;

        var xform = Transform(uid);

        if (!GridQuery.HasComp(xform.MapUid))
            return false;

        if (IsSpaceTile(uid, xform))
        {
            ground = NoGround;
            return false;
        }

        if (!zPhys.IgnoreHighGround)
        {
            ground = ComputeGroundHeightInternal((uid, zPhys), out _);
            return true;
        }

        return true;
    }

    private void UpdateGrounded(EntityUid uid, CEZPhysicsComponent zPhys, bool hasGround,
        float groundHeight, float frameTime, out bool landed)
    {
        landed = false;

        if (!hasGround || groundHeight == NoGround)
        {
            if (zPhys.IsGrounded)
            {
                zPhys.IsGrounded = false;
                DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.IsGrounded));
            }

            if (hasGround)
            {
                if (zPhys.Velocity >= 0)
                    zPhys.Velocity = -1f;

                if (IsGravityEnabled(uid))
                    zPhys.Velocity -= ZGravityForce * zPhys.GravityMultiplier * frameTime;

                HandleLevelChange(uid, zPhys, false);
            }

            return;
        }

        var distanceToGround = zPhys.LocalPosition - groundHeight;

        var currentlyGrounded = (distanceToGround <= 0.05f || zPhys.CurrentStickyGround)
            && distanceToGround <= MaxStepHeight;

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
        if (zPhys.IsGrounded)
            return;

        if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
        {
            _queuedLandings.Add(uid, -zPhys.Velocity);
        }

        zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
    }

    [PublicAPI]
    public bool IsSpaceTile(EntityUid uid, TransformComponent xform)
    {
        var mapId = xform.MapID;
        var worldPos = _transform.GetWorldPosition(uid);

        if (!_mapManager.TryFindGridAt(mapId, worldPos, out var gridUid, out var grid))
            return false;

        var gridXform = Transform(gridUid);
        var localPos = _transform.GetInvWorldMatrix(gridXform);

        var realpos = Vector2.Transform(worldPos, localPos);

        var indices = new Vector2i(
            (int)Math.Floor(realpos.X / grid.TileSize),
            (int)Math.Floor(realpos.Y / grid.TileSize)
        );

        if (!MapSys.TryGetTileRef(gridUid, grid, indices, out var tileRef))
            return false;

        if (!TilDefMan.TryGetDefinition(tileRef.Tile.TypeId, out var def))
            return false;

        return def.ID == ZTileID;
    }

    [PublicAPI]
    public bool IsSpaceTile(Vector2i indices, EntityUid? mapUid)
    {
        if (!GridQuery.TryComp(mapUid, out var grid))
            return false;

        if (!MapSys.TryGetTileRef(mapUid.Value, grid, indices, out var tileRef))
            return false;

        if (!TilDefMan.TryGetDefinition(tileRef.Tile.TypeId, out var def))
            return false;

        return def.ID == ZTileID;
    }

    private void HandleLevelChange(EntityUid uid, CEZPhysicsComponent zPhys, bool hasGround)
    {
        if (zPhys.LocalPosition < 0)
        {
            var xform = Transform(uid);

            if (!_zMapQuery.TryComp(xform.MapUid, out var zMap))
            {
                StopFall(uid, zPhys);
                return;
            }

            if (!TryMapOffset((xform.MapUid.Value, zMap), -1, out var below) || below == null)
            {
                StopFall(uid, zPhys);
                return;
            }

            if (_config.GetCVar(UCCVars.FallToBackroomsEnabled))
            {
                StopFall(uid, zPhys);
                return;
            }

            if (!TryMoveDownOrChasm(uid))
            {
                StopFall(uid, zPhys);
                return;
            }

            zPhys.LocalPosition += 1;

            if (zPhys.CurrentStickyGround)
                return;

            if (!IsGravityEnabled(uid))
            {
                zPhys.Velocity = 0;
            }

            var fallEv = new CEZLevelFallMapEvent();
            RaiseLocalEvent(uid, fallEv);

            return;
        }
        else if (zPhys.LocalPosition >= 1)
        {
            if (HasTileAbove(uid))
            {
                if (MathF.Abs(zPhys.Velocity) >= ImpactVelocityLimit)
                    _queuedLandings.Add(uid, zPhys.Velocity);

                zPhys.LocalPosition = 1;
                zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
            }
            else
            {
                if (TryMoveUp(uid))
                    zPhys.LocalPosition -= 1;
            }
        }
    }

    private void StopFall(EntityUid uid, CEZPhysicsComponent zPhys)
    {
        zPhys.LocalPosition = 0;
        SetZVelocity((uid, zPhys), 0);

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetLinearVelocity(uid, Vector2.Zero);
            Dirty(uid, physics);
        }

        if (!zPhys.IsGrounded)
        {
            zPhys.IsGrounded = true;
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.IsGrounded));
        }
    }

    private bool IsGravityEnabled(EntityUid uid)
    {
        if (HasComp<GravityAffectedComponent>(uid))
            return !_gravity.IsWeightless((uid, Comp<GravityAffectedComponent>(uid)));

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
                !tileRef.Tile.IsEmpty || !IsSpaceTile(worldPosI, checkingMap))
            {
                return -floor;
            }
        }

        return -maxFloors;
    }
}
