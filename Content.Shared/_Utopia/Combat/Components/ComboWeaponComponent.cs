using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ComboWeaponComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<CombatWeaponMovePrototype>> AvailableMoves { get; private set; } = new();

    [AutoNetworkedField]
    public List<WeaponCombatAction> CurrestActions = new();

    public TimeSpan ResetTime = TimeSpan.FromSeconds(6);

    public ComboWeaponStand CurrentStand = ComboWeaponStand.Offensive;

    [DataField]
    public SoundSpecifier? SwapSound;
}
