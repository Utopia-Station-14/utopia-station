using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.ModSuits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitModuleComponent : Component
{
    [DataField]
    public int Complexity;

    [DataField]
    public float Expenditure = 0.2f;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public bool Toggleable = true;

    [DataField]
    public ModSuitPart? RequiredPart;

    [DataField]
    public EntProtoId? Action;

    [DataField]
    public string? Key;

    [ViewVariables]
    public bool Active;

    [ViewVariables]
    public EntityUid? ActionEntity;
}
