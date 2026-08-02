using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.ZLevels.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Gravity;

public sealed class GravityGeneratorSystem : SharedGravityGeneratorSystem
{
    [Dependency] private readonly GravitySystem _gravitySystem = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!; // Utopia-Tweak : ZLevels

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GravityGeneratorComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<GravityGeneratorComponent, ChargedMachineActivatedEvent>(OnActivated);
        SubscribeLocalEvent<GravityGeneratorComponent, ChargedMachineDeactivatedEvent>(OnDeactivated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<GravityGeneratorComponent, PowerChargeComponent>();
        while (query.MoveNext(out var uid, out var grav, out var charge))
        {
            if (!_lights.TryGetLight(uid, out var pointLight))
                continue;

            _lights.SetEnabled(uid, charge.Charge > 0, pointLight);
            _lights.SetRadius(uid, MathHelper.Lerp(grav.LightRadiusMin, grav.LightRadiusMax, charge.Charge),
                pointLight);
        }
    }

    private void OnActivated(Entity<GravityGeneratorComponent> ent, ref ChargedMachineActivatedEvent args)
    {
        ent.Comp.GravityActive = true;
        Dirty(ent, ent.Comp);

        var xform = Transform(ent);

        // Utopia-Tweak : ZLevels
        foreach (var gridUid in GetTargetGrids(xform.ParentUid))
        {
            if (TryComp(gridUid, out GravityComponent? gravity))
            {
                _gravitySystem.EnableGravity(gridUid, gravity);
            }
        }
        // Utopia-Tweak : ZLevels
    }

    private void OnDeactivated(Entity<GravityGeneratorComponent> ent, ref ChargedMachineDeactivatedEvent args)
    {
        ent.Comp.GravityActive = false;
        Dirty(ent, ent.Comp);

        var xform = Transform(ent);

        // Utopia-Tweak : ZLevels
        foreach (var gridUid in GetTargetGrids(xform.ParentUid))
        {
            if (TryComp(gridUid, out GravityComponent? gravity))
            {
                _gravitySystem.RefreshGravity(gridUid, gravity);
            }
        }
        // Utopia-Tweak : ZLevels
    }

    private void OnParentChanged(EntityUid uid, GravityGeneratorComponent component, ref EntParentChangedMessage args)
    {
        // Utopia-Tweak : ZLevels
        if (component.GravityActive && args.OldParent.HasValue)
        {
            foreach (var gridUid in GetTargetGrids(args.OldParent.Value))
            {
                if (TryComp(gridUid, out GravityComponent? gravity))
                {
                    _gravitySystem.RefreshGravity(gridUid, gravity);
                }
            }
        }
        // Utopia-Tweak : ZLevels
    }

    // Utopia-Tweak : ZLevels
    private HashSet<EntityUid> GetTargetGrids(EntityUid parentUid)
    {
        var grids = new HashSet<EntityUid> { parentUid };
        if (!TryComp<GridMotionLinkComponent>(parentUid, out var motionLink))
            return grids;

        var targetGroupId = motionLink.GroupId;

        if (!TryComp(parentUid, out TransformComponent? parentXform) || parentXform.MapUid == null)
            return grids;

        if (!_zLevels.TryGetZNetwork(parentXform.MapUid.Value, out var net) || net == null)
            return grids;

        var validMaps = new HashSet<EntityUid>();
        foreach (var level in net.Value.Comp.ZLevels)
        {
            if (level.Value is { Valid: true } map)
            {
                validMaps.Add(map);
            }
        }

        var query = EntityQueryEnumerator<MapGridComponent, TransformComponent, GridMotionLinkComponent>();
        while (query.MoveNext(out var gridUid, out var _, out var gridXform, out var linkComp))
        {
            if (linkComp.GroupId == targetGroupId && gridXform.MapUid != null && validMaps.Contains(gridXform.MapUid.Value))
            {
                grids.Add(gridUid);
            }
        }

        return grids;
    }
    // Utopia-Tweak : ZLevels
}
