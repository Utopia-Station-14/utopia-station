using Content.Shared._Utopia.Combat;

namespace Content.Client._Utopia.Combat;

public sealed partial class ClientComboSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComboComponent, GetPerformedAttackTypesEvent>(OnGetAttackTypes);
        SubscribeLocalEvent<ComboWeaponComponent, GetPerformedAttackTypesEvent>(OnGetWAttackTypes);
    }

    private void OnGetAttackTypes(Entity<ComboComponent> ent, ref GetPerformedAttackTypesEvent args)
    {
        args.AttackTypes = ent.Comp.CurrestActions;
    }

    private void OnGetWAttackTypes(Entity<ComboWeaponComponent> ent, ref GetPerformedAttackTypesEvent args)
    {
        args.WAttackTypes = ent.Comp.CurrestActions;
    }
}
