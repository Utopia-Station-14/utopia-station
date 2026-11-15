using Content.Shared.Projectiles;
using Content.Server._Utopia.Deflector;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server.ADT.Deflector;

public sealed class DeflectorSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeflectorComponent, ProjectileReflectAttemptEvent>(OnProjectileReflectAttempt);
        SubscribeLocalEvent<DeflectorComponent, HitScanReflectAttemptEvent>(OnHitScanReflectAttempt);
    }

    private void OnProjectileReflectAttempt(EntityUid uid, DeflectorComponent comp, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var proj = args.ProjUid;

        if (!TryComp<TransformComponent>(uid, out var xform) || !TryComp<GunComponent>(uid, out var gun))
            return;

        if (!TryGetImpactDirection(proj, xform, out var dir)
            || !TryComp<ReflectiveComponent>(proj, out var refl) || refl.Reflective == 0
            || comp.ExitSide.Contains(dir)
            || !TryGetOffset(comp, dir, out var offset))
        {
            return;
        }

        args.Cancelled = true;

        var newPos = xform.LocalPosition + xform.LocalRotation.RotateVec(offset);
        _xform.SetLocalPosition(proj, newPos, xform);

        _gun.Shoot((uid, gun), proj, xform.Coordinates, new EntityCoordinates(uid, offset), out _, uid);
    }

    private void OnHitScanReflectAttempt(EntityUid uid, DeflectorComponent comp, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected)
            return;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        var invWorldRot = -_xform.GetWorldRotation(xform);
        var localIncomingDir = invWorldRot.RotateVec(-args.Direction);
        var dir = localIncomingDir.ToAngle().GetCardinalDir();

        if (comp.ExitSide.Contains(dir) || !TryGetOffset(comp, dir, out var offset))
            return;

        args.Reflected = true;

        var worldRotation = _xform.GetWorldRotation(xform);
        args.Direction = worldRotation.RotateVec(offset).Normalized();
    }

    private bool TryGetImpactDirection(EntityUid proj, TransformComponent mirrorXform, out Direction dir)
    {
        var local = Vector2.Transform(
            _xform.GetWorldPosition(proj),
            _xform.GetInvWorldMatrix(mirrorXform));

        dir = local.ToAngle().GetCardinalDir();
        return true;
    }

    private bool TryGetOffset(DeflectorComponent comp, Direction dir, out Vector2 offset)
    {
        if (comp.TrinaryReflection && comp.TrinaryMirrorDirection is { } vec)
        {
            offset = vec.ToVec();
            return offset != default;
        }

        if (comp.BinaryReflection && DeflectorComponent.DirectionToVector.TryGetValue(dir, out var binVec))
        {
            offset = binVec;
            return offset != default;
        }

        offset = default;
        return false;
    }
}
