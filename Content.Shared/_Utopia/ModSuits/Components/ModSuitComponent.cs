using Content.Shared.Tools;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.ModSuits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitComponent : Component
{

    [DataField]
    public EntProtoId? HelmetPrototype;

    [DataField]
    public EntProtoId? BodyPrototype;

    [DataField]
    public EntProtoId? GlovesPrototype;

    [DataField]
    public EntProtoId? BootsPrototype;

    [DataField]
    public float PartSwapCost = 2f;

    [DataField]
    public float DeployDelay = 0.5f;

    [DataField]
    public float ManualRemoveDelay = 15f;

    [DataField]
    public float SpeedDraw = 0.5f;

    [DataField]
    public ProtoId<ToolQualityPrototype> PanelTool = "Screwing";

    [DataField]
    public float PanelDelay = 2f;

    [DataField]
    public EntProtoId Action = "UtopiaActionToggleModSuit";

    [DataField]
    public string PartsContainerId = "modsuit-parts";

    [DataField]
    public string ModulesContainerId = "modsuit-modules";



    [DataField, AutoNetworkedField]
    public bool PanelOpen;

    [ViewVariables, AutoNetworkedField]
    public bool Locked;

    [ViewVariables, AutoNetworkedField]
    public int EquippedParts;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Wearer;

    [ViewVariables, AutoNetworkedField]
    public Dictionary<ModSuitPart, EntityUid> Parts = new();


    [ViewVariables]
    public bool SpeedEnabled;

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public Container PartsContainer = default!;

    [ViewVariables]
    public Container ModulesContainer = default!;

    [ViewVariables]
    public TimeSpan NextUiUpdate;
}
