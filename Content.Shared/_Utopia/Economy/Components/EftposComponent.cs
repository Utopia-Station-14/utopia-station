using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy;

[RegisterComponent]
public sealed partial class EftposComponent : Component
{
    [ViewVariables]
    public int? BankAccountId;

    [ViewVariables]
    public int Amount;

    [DataField]
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/_Utopia/Machines/chime.ogg");

    [DataField]
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/_Utopia/Machines/buzz-sigh.ogg");
}

[Serializable, NetSerializable]
public enum EftposKey
{
    Key
}
