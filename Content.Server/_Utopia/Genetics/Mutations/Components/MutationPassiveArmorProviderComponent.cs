using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Utopia.Genetics.Mutations.Components;

[RegisterComponent]
public sealed partial class MutationPassiveArmorProviderComponent : Component
{
    [DataField(required: true)]
    public ProtoId<DamageModifierSetPrototype> ModifierSetId = default!;
}
