using Content.Shared._Utopia.ModSuits;
using Content.Shared._Utopia.ModSuits.Modules.Flashlight;
using Robust.Shared.GameObjects;

namespace Content.Server._Utopia.ModSuits.Modules.Flashlight;

public sealed partial class ModSuitFlashlightModuleSystem : EntitySystem
{
    [Dependency] private SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitFlashlightModuleComponent, ModSuitModuleActivatedEvent>(OnActivated);
        SubscribeLocalEvent<ModSuitFlashlightModuleComponent, ModSuitModuleDeactivatedEvent>(OnDeactivated);
    }

    private void OnActivated(Entity<ModSuitFlashlightModuleComponent> ent, ref ModSuitModuleActivatedEvent args)
    {
        if (args.Part is not { } part || TerminatingOrDeleted(part))
            return;

        var light = _light.EnsureLight(part);

        _light.SetRadius(part, ent.Comp.Radius, light);
        _light.SetEnergy(part, ent.Comp.Energy, light);
        _light.SetColor(part, ent.Comp.Color, light);
        _light.SetEnabled(part, true, light);
    }

    private void OnDeactivated(Entity<ModSuitFlashlightModuleComponent> ent, ref ModSuitModuleDeactivatedEvent args)
    {
        if (args.Part is not { } part || TerminatingOrDeleted(part))
            return;

        if (!_light.TryGetLight(part, out var light))
            return;

        _light.SetEnabled(part, false, light);
    }
}
