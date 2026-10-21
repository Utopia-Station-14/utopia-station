using Content.Shared._Utopia.ModSuits;
using Content.Shared._Utopia.ModSuits.Modules.EyeProtection;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Flash.Components;

namespace Content.Server._Utopia.ModSuits.Modules.EyeProtection;

public sealed class ModSuitEyeProtectionModuleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModSuitEyeProtectionModuleComponent, ModSuitModuleActivatedEvent>(OnActivated);
        SubscribeLocalEvent<ModSuitEyeProtectionModuleComponent, ModSuitModuleDeactivatedEvent>(OnDeactivated);
    }

    private void OnActivated(Entity<ModSuitEyeProtectionModuleComponent> ent, ref ModSuitModuleActivatedEvent args)
    {
        if (args.Part is not { } part || TerminatingOrDeleted(part))
            return;

        if (!HasComp<FlashImmunityComponent>(part))
        {
            AddComp<FlashImmunityComponent>(part);
            ent.Comp.AddedFlashImmunity = true;
        }

        if (!HasComp<EyeProtectionComponent>(part))
        {
            AddComp<EyeProtectionComponent>(part);
            ent.Comp.AddedEyeProtection = true;
        }
    }

    private void OnDeactivated(Entity<ModSuitEyeProtectionModuleComponent> ent, ref ModSuitModuleDeactivatedEvent args)
    {
        if (args.Part is not { } part || TerminatingOrDeleted(part))
            return;

        if (ent.Comp.AddedFlashImmunity)
        {
            RemComp<FlashImmunityComponent>(part);
            ent.Comp.AddedFlashImmunity = false;
        }

        if (ent.Comp.AddedEyeProtection)
        {
            RemComp<EyeProtectionComponent>(part);
            ent.Comp.AddedEyeProtection = false;
        }
    }
}
