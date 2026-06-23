using Robust.Shared.GameStates;

namespace Content.Shared._Utopia.Genetics.Mutations.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class MutationDoAfterModifierComponent : Component
{
    [DataField(required: true)]
    public float Multiplier = 1f;
}
