using Content.Shared._Utopia.ZLevels.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Shared._Utopia.ZLevels.Systems;

public sealed class GridMotionPhysicsSyncSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    private const string GlobalGroupId = "ZZZ";

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(SharedPhysicsSystem));
    }

    public void InitializeGrid(EntityUid gridUid)
    {
        var link = EnsureComp<GridMotionLinkComponent>(gridUid);
        link.GroupId = GlobalGroupId;
    }

    private void RelayMotion(EntityUid uid, GridMotionLinkComponent comp)
    {
        var query = EntityQueryEnumerator<GridMotionLinkComponent, PhysicsComponent>();

        // Collect matching bodies to avoid mutating while enumerating.
        var matches = new List<(EntityUid uid, GridMotionLinkComponent link, PhysicsComponent body)>();
        Vector2 linear = Vector2.Zero;
        float angular = 0f;

        while (query.MoveNext(out var targetUid, out var link, out var phys))
        {
            if (link.GroupId != comp.GroupId)
                continue;

            matches.Add((targetUid, link, phys));
            linear += phys.LinearVelocity;
            angular += phys.AngularVelocity;
        }

        if (matches.Count == 0)
            return;

        linear /= matches.Count;
        angular /= matches.Count;

        if (linear == Vector2.Zero && angular == 0f)
            return;

        foreach (var (targetUid, link, phys) in matches)
        {
            if (link.GroupId != comp.GroupId)
                continue;

            _physics.SetLinearVelocity(targetUid, linear, body: phys);
            _physics.SetAngularVelocity(targetUid, angular, body: phys);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GridMotionLinkComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var comp, out var phys))
        {
            if (!comp.IsSource)
                continue;

            RelayMotion(uid, comp);
        }
    }

    public void SetGridPosition(EntityUid origin, Vector2 position, GridMotionLinkComponent? link = null)
    {
        if (!Resolve(origin, ref link, false))
        {
            _transformSystem.SetWorldPosition(origin, position);
            return;
        }

        var diff = position - _transformSystem.GetWorldPosition(origin);

        var query = EntityQueryEnumerator<GridMotionLinkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (link.GroupId != comp.GroupId)
                continue;

            var targetPos = _transformSystem.GetWorldPosition(uid) + diff;
            _transformSystem.SetWorldPosition(uid, targetPos);
            _physics.SetLinearVelocity(uid, Vector2.Zero);
        }
    }
}
