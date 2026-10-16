using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Toxicology.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ExplosionBeaconConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? BeaconEntity;

    [DataField]
    public ProtoId<SourcePortPrototype> LinkingPort = "ExplosionBeaconConsoleSender";
}
