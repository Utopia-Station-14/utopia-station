using Content.Shared._Utopia.ZLevels.Components;
using Content.Shared._Utopia.ZLevels.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Map;

namespace Content.Server._Utopia.ZLevels.Systems;

public sealed class GridMotionAutoSyncSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private readonly Dictionary<MapId, EntityUid> _masters = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridMotionLinkComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GridMotionLinkComponent, GridMotionSyncEvent>(OnMotionSync);
    }

    private void OnStartup(EntityUid uid, GridMotionLinkComponent comp, ComponentStartup args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var mapId = xform.MapID;

        if (comp.IsMaster)
        {
            _masters[mapId] = uid;
            return;
        }

        if (_masters.TryGetValue(mapId, out var master))
        {
            comp.MasterGrid = master;
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<
            GridMotionLinkComponent,
            PhysicsComponent,
            TransformComponent>();

        while (query.MoveNext(out var uid, out var link, out var phys, out var xform))
        {
            if (!link.IsMaster)
                continue;

            if (!phys.Awake)
                continue;

            var ev = new GridMotionSyncEvent
            {
                LinearVelocity = phys.LinearVelocity,
                AngularVelocity = phys.AngularVelocity,
                Rotation = xform.WorldRotation
            };

            var slaves = EntityQueryEnumerator<GridMotionLinkComponent, TransformComponent>();
            while (slaves.MoveNext(out var slaveUid, out var slaveLink, out var slaveXform))
            {
                if (slaveLink.IsMaster)
                    continue;

                if (slaveXform.MapID != xform.MapID)
                    continue;

                if (slaveLink.MasterGrid == null)
                    slaveLink.MasterGrid = uid;

                if (slaveLink.MasterGrid != uid)
                    continue;

                RaiseLocalEvent(slaveUid, ev);
            }
        }
    }

    private void OnMotionSync(
        EntityUid uid,
        GridMotionLinkComponent comp,
        GridMotionSyncEvent ev)
    {
        if (comp.IsMaster)
            return;

        if (!TryComp(uid, out PhysicsComponent? phys))
            return;

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        _physics.SetLinearVelocity(uid, ev.LinearVelocity);
        _physics.SetAngularVelocity(uid, ev.AngularVelocity);

        xform.WorldRotation = ev.Rotation;
    }
}
