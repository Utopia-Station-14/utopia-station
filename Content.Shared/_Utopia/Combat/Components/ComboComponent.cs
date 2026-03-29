using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Combat;

[RegisterComponent, NetworkedComponent]
public sealed partial class ComboComponent : Component
{
    [DataField(required: true)]
    public HashSet<ProtoId<CombatMovePrototype>> AvailableMoves { get; private set; } = new();

    public List<CombatAction> CurrestActions { get; private set; } = new();

    public EntityUid? Target = default;
}
