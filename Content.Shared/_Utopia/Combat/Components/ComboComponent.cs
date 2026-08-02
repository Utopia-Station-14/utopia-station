using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ComboComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<CombatMovePrototype>> AvailableMoves { get; private set; } = new();

    [AutoNetworkedField]
    public List<CombatAction> CurrestActions = new();

    public TimeSpan ResetTime = TimeSpan.FromSeconds(3);
}

[ByRefEvent]
public record struct GetPerformedAttackTypesEvent(List<CombatAction>? AttackTypes = null);
