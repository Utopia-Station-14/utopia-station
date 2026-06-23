using Content.Shared._RMC14.Weapons.Common;
using Robust.Shared.Audio.Systems;
using Content.Shared._Utopia.Combat;

namespace Content.Server._Utopia.Combat;

public sealed class WeaponComboSystem : SharedWeaponComboSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboWeaponComponent, UniqueActionEvent>(OnUniqueAction);
    }

    private void OnUniqueAction(Entity<ComboWeaponComponent> entity, ref UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        entity.Comp.CurrentStand = entity.Comp.CurrentStand switch
        {
            ComboWeaponStand.Protective => ComboWeaponStand.Offensive,
            ComboWeaponStand.Offensive => ComboWeaponStand.Protective,
            _ => entity.Comp.CurrentStand
        };

        if (TryComp<AppearanceComponent>(entity, out var appearanceComponent))
        {
            _appearance.SetData(entity, ComboWeaponState.State, entity.Comp.CurrentStand,
                appearanceComponent);
        }

        if (entity.Comp.SwapSound != null)
        {
            _audio.PlayPvs(entity.Comp.SwapSound, entity);
        }

        args.Handled = true;
    }
}

