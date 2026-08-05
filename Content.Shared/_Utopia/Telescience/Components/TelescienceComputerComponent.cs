using System.Numerics;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Telescience.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class TelescienceComputerComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Vector2 Position;

    [ViewVariables]
    public ProtoId<SourcePortPrototype> LinkingPort = "Output";

    [ViewVariables, AutoNetworkedField]
    public EntityUid? TeleporterUid;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan CooldownTime = TimeSpan.Zero;

    [DataField]
    public int Crystals = 0;
}
