using Content.Shared._Utopia.ModSuits;
using Robust.Shared.Containers;

namespace Content.Server._Utopia.ModSuits.Systems;

[RegisterComponent]
public sealed partial class ModSuitPartComponent : Component
{
    [ViewVariables]
    public EntityUid Suit;

    [ViewVariables]
    public ModSuitPart Part;

    [ViewVariables]
    public bool Retracting;

    [DataField]
    public string ReplacedContainerId = "modsuit-replaced";

    [ViewVariables]
    public ContainerSlot Replaced = default!;
}
