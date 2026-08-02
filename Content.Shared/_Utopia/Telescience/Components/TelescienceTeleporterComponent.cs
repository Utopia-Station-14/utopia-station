using System.Numerics;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Telescience.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TelescienceTeleporterComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Vector2 Position;

    [DataField]
    public float TeleportSize = 0.5f;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan Cooldown;

    [DataField, AutoNetworkedField]
    public TimeSpan CooldownInterval = TimeSpan.FromSeconds(10);

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Computer;

    [DataField]
    public float TeleportMaxDistance = 250f;
    [DataField]
    public float BaseTeleportMaxDistance = 250f;

    [DataField]
    public EntProtoId PortalEnt = "UtopiaWormhole";

    [ViewVariables]
    public EntityUid?[] Portals = new EntityUid?[2];

    [DataField]
    public ProtoId<MachinePartPrototype> MachinePartAddDistance = "Manipulator";

    [DataField]
    public float PartTierAddDistanceMultiplier = 1.25f;
}
