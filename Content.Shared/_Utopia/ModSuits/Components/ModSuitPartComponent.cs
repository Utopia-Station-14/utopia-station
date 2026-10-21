using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.ModSuits;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModSuitPartComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Suit;

    [ViewVariables, AutoNetworkedField]
    public ModSuitPart Part;

    [ViewVariables]
    public bool Retracting;

    [DataField]
    public string ReplacedContainerId = "modsuit-replaced";

    [ViewVariables]
    public ContainerSlot Replaced = default!;
}
