using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationRadiationResistanceComponent : Component
{
    [DataField("modifier")]
    public ProtoId<DamageModifierSetPrototype> ModifierSetId = "RadiationResistance";
}
