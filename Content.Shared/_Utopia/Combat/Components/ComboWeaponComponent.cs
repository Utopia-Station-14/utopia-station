using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

[RegisterComponent]
public sealed partial class ComboWeaponComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<CombatWeaponMovePrototype>> AvailableMoves { get; private set; } = new();

    public List<WeaponCombatAction> CurrestActions { get; private set; } = new();

    public ComboWeaponStand CurrentStand = ComboWeaponStand.Offensive;

    [DataField]
    public SoundSpecifier? SwapSound;
}
