using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

[Prototype]
public sealed partial class CombatMovePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<CombatAction> ActionsNeeds { get; private set; } = new();

    [DataField]
    public List<IComboEffect> ComboEvent = new();
}

[Prototype]
public sealed partial class CombatWeaponMovePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<WeaponCombatAction> ActionsNeeds { get; private set; } = new();

    [DataField]
    public List<IComboEffect> ComboEvent = new();
}
