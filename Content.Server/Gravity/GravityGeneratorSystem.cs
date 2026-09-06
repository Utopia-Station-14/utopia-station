using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.Gravity;

namespace Content.Server.Gravity;

public sealed partial class GravityGeneratorSystem : SharedGravityGeneratorSystem
{
    [Dependency] private GravitySystem _gravitySystem = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!; // Utopia-Tweak : ZLevels

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
        foreach (var gridUid in _zLevels.GetTargetGrids(xform.ParentUid))
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
        foreach (var gridUid in _zLevels.GetTargetGrids(xform.ParentUid))
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
            foreach (var gridUid in _zLevels.GetTargetGrids(args.OldParent.Value))
            {
                if (TryComp(gridUid, out GravityComponent? gravity))
                {
                    _gravitySystem.RefreshGravity(gridUid, gravity);
                }
            }
        }
        // Utopia-Tweak : ZLevels
    }
}
