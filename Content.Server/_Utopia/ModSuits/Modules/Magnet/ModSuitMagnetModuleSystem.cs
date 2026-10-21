using Content.Shared._Utopia.ModSuits;
using Content.Shared._Utopia.ModSuits.Modules.Magnet;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;

namespace Content.Server._Utopia.ModSuits.Modules.Magnet;

public sealed partial class ModSuitMagnetModuleSystem : EntitySystem
{
    [Dependency] private SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitMagnetModuleComponent, ModSuitModuleActivatedEvent>(OnActivated);
        SubscribeLocalEvent<ModSuitMagnetModuleComponent, ModSuitModuleDeactivatedEvent>(OnDeactivated);
    }

    private void OnActivated(Entity<ModSuitMagnetModuleComponent> ent, ref ModSuitModuleActivatedEvent args)
    {
        if (args.Part is { } part && !TerminatingOrDeleted(part))
            EnsureComp<ModSuitMagnetActiveComponent>(part);

        SetEffects(ent, args.Wearer, on: true);
    }

    private void OnDeactivated(Entity<ModSuitMagnetModuleComponent> ent, ref ModSuitModuleDeactivatedEvent args)
    {
        if (args.Part is { } part && !TerminatingOrDeleted(part))
            RemComp<ModSuitMagnetActiveComponent>(part);

        SetEffects(ent, args.Wearer, on: false);
    }

    private void SetEffects(Entity<ModSuitMagnetModuleComponent> ent, EntityUid? wearer, bool on)
    {
        if (wearer is not { } uid || TerminatingOrDeleted(uid))
            return;

        if (TryComp<MovedByPressureComponent>(uid, out var moved))
        {
            if (on && moved.Enabled)
            {
                moved.Enabled = false;
                ent.Comp.DisabledPressure = true;
            }
            else if (!on && ent.Comp.DisabledPressure)
            {
                moved.Enabled = true;
                ent.Comp.DisabledPressure = false;
            }
        }

        _gravity.RefreshWeightless(uid);
    }
}
