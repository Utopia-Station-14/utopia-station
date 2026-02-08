using Robust.Shared.Audio;

namespace Content.Server._Utopia.Construction.Components;

[RegisterComponent]
public sealed partial class PartExchangerComponent : Component
{
    [DataField]
    public float ExchangeDuration = 3;

    [DataField]
    public bool DoDistanceCheck = true;

    [DataField]
    public SoundSpecifier ExchangeSound = new SoundPathSpecifier("/Audio/Items/rped.ogg");

    public EntityUid? AudioStream;
}
