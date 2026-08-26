using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Whitelist;
using Content.Shared._Utopia.Deflector;
using Robust.Shared.Map;

namespace Content.Server._Utopia.Deflector;

public sealed class ProjectileMirrorSystem : EntitySystem
{
    /// <summary>
    /// TODO: Почините проджектайлы суки блять.
    /// Нет, иди нахуй чмо by m&b.
    /// </summary>
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeflectorComponent, ProjectileReflectAttemptEvent>(OnReflectAttempt);
        SubscribeLocalEvent<DeflectorComponent, HitScanReflectAttemptEvent>(OnHitscanReflectAttempt);
    }

    private void OnHitscanReflectAttempt(EntityUid uid, DeflectorComponent comp, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected || args.Reflective == ReflectType.None)
            return;

        if (_whitelist.IsWhitelistFail(comp.Whitelist, args.SourceItem))
            return;

        var impactDir = GetHitscanImpactDirection(uid, args.Direction);
        if (comp.ExitSide.Contains(impactDir.ToString()))
            return;

        if (!TryGetOffset(comp, impactDir, out var offset))
            return;

        args.Reflected = true;
        var worldRot = _xform.GetWorldRotation(uid);
        args.Direction = worldRot.RotateVec(offset).Normalized();
    }

    private Direction GetHitscanImpactDirection(EntityUid mirror, Vector2 incomingWorldDirection)
    {
        var worldRot = _xform.GetWorldRotation(mirror);
        var localDir = (-worldRot).RotateVec(incomingWorldDirection);

        return (-localDir).ToAngle().GetCardinalDir();
    }

    private void OnReflectAttempt(EntityUid uid, DeflectorComponent comp, ref ProjectileReflectAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var proj = args.ProjUid;
        if (!TryComp<GunComponent>(uid, out var gun))
            return;

        if (!TryComp<ReflectiveComponent>(proj, out var reflective) || reflective.Reflective == 0)
            return;

        if (_whitelist.IsWhitelistFail(comp.Whitelist, proj))
            return;

        var dir = GetImpactDirection(uid, proj);
        if (comp.ExitSide.Contains(dir.ToString()))
            return;

        if (!TryGetOffset(comp, dir, out var offset))
            return;

        args.Cancelled = true;
        var xform = Transform(uid);
        var newPos = xform.LocalPosition + xform.LocalRotation.RotateVec(offset);

        _xform.SetLocalPosition(proj, newPos);
        _gun.Shoot((uid, gun), proj, xform.Coordinates, new EntityCoordinates(uid, offset), out _, uid);
    }

    private Direction GetImpactDirection(EntityUid mirror, EntityUid proj)
    {
        var local = Vector2.Transform(
            _xform.GetWorldPosition(proj),
            _xform.GetInvWorldMatrix(Transform(mirror)));

        return local.ToAngle().GetCardinalDir();
    }

    private static bool TryGetOffset(DeflectorComponent comp, Direction dir, out Vector2 offset)
    {
        if (comp.TrinaryReflection && comp.TrinaryMirrorDirection is { } fixedDir)
        {
            offset = fixedDir.ToVec();
            return true;
        }

        if (comp.BinaryReflection && DeflectorComponent.DirectionToVector.TryGetValue(dir, out var mappedOffset))
        {
            offset = mappedOffset;
            return true;
        }

        offset = default;
        return false;
    }
}
