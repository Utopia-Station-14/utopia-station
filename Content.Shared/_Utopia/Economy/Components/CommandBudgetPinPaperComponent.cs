using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.Economy.Components;

[RegisterComponent]
public sealed partial class CommandBudgetPinPaperComponent : Component
{
    [DataField]
    public ProtoId<CargoAccountPrototype>? CommandBudgetType;
}
