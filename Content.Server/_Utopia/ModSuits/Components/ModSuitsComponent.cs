using Content.Shared._Utopia.ModSuits;
using Content.Shared.Tools;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.ModSuits.Systems;

[RegisterComponent]
public sealed partial class ModSuitComponent : Component
{
    [DataField]
    public float BaseDraw = 0.2f;

    [DataField]
    public float DrawPerPart = 0.1f;

    [ViewVariables]
    public bool Locked;

    [ViewVariables]
    public int EquippedParts;

    [DataField]
    public EntProtoId? HelmetPrototype;
    [DataField]
    public EntProtoId? BodyPrototype;
    [DataField]
    public EntProtoId? GlovesPrototype;
    [DataField]
    public EntProtoId? BootsPrototype;

    [DataField] public bool PanelOpen;
    [DataField] public ProtoId<ToolQualityPrototype> PanelTool = "Screwing";
    [DataField] public float PanelDelay = 2f;

    [DataField]
    public EntProtoId Action = "UtopiaActionToggleModSuit";

    [DataField]
    public string PartsContainerId = "modsuit-parts";

    [DataField]
    public string ModulesContainerId = "modsuit-modules";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [ViewVariables]
    public EntityUid? Wearer;

    [ViewVariables]
    public Dictionary<ModSuitPart, EntityUid> Parts = new();

    [ViewVariables]
    public Container PartsContainer = default!;

    [ViewVariables]
    public Container ModulesContainer = default!;

    [ViewVariables]
    public TimeSpan NextUiUpdate;

    [DataField]
    public float DeployDelay = 0.5f;
}
