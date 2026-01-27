using Content.Shared._Utopia.ZLevels.Components;
using Content.Shared._Utopia.ZLevels.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using System.Numerics;

namespace Content.Server._Utopia.ZLevels.Systems;

public sealed class GridMotionPhysicsSyncSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private const string GlobalGroupId = "ZZZ";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridMotionLinkComponent, GridMotionRelayEvent>(OnRelayMotion);
    }

    public void InitializeGrid(EntityUid gridUid)
    {
        var link = EnsureComp<GridMotionLinkComponent>(gridUid);
        link.GroupId = GlobalGroupId;
    }

    private void OnRelayMotion(EntityUid uid, GridMotionLinkComponent link, GridMotionRelayEvent ev)
    {
        var query = EntityQueryEnumerator<GridMotionLinkComponent, PhysicsComponent>();

        while (query.MoveNext(out var targetUid, out var targetLink, out var phys))
        {
            if (targetLink.GroupId != link.GroupId)
                continue;

            if (targetUid == ev.SourceGrid)
                continue;

            _physics.SetLinearVelocity(targetUid, ev.LinearVelocity, body: phys);
            _physics.SetAngularVelocity(targetUid, ev.AngularVelocity, body: phys);
        }
    }

    public void RelayMovement(EntityUid sourceGrid, Vector2 linear, float angular)
    {
        var ev = new GridMotionRelayEvent(sourceGrid, linear, angular);
        RaiseLocalEvent(sourceGrid, ev, broadcast: true);
    }
}