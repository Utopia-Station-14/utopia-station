using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class ChemicalResistanceComponent : Component
{
    [DataField]
    public List<ProtoId<ReagentPrototype>> Reagents { get; private set; } = new();

    [DataField]
    public FixedPoint2 PurgeAmount { get; private set; } = FixedPoint2.New(1);
}
