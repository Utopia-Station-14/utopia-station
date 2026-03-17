using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Economy;

[RegisterComponent]
public sealed partial class ATMComponent : Component
{
    [DataField]
    public string CurrencyType = "SpaceCash";

    public string SlotId = "BankCardSlot";

    public ProtoId<StackPrototype> CreditStackPrototype = "Credit";

    [DataField]
    public SoundSpecifier SoundInsertCurrency = new SoundPathSpecifier("/Audio/_Utopia/Machines/polaroid2.ogg");

    [DataField]
    public SoundSpecifier SoundWithdrawCurrency = new SoundPathSpecifier("/Audio/_Utopia/Machines/polaroid1.ogg");

    [DataField]
    public SoundSpecifier SoundApply = new SoundPathSpecifier("/Audio/_Utopia/Machines/chime.ogg");

    [DataField]
    public SoundSpecifier SoundDeny = new SoundPathSpecifier("/Audio/_Utopia/Machines/buzz-sigh.ogg");

    [DataField]
    public float ErrorChance = 0.25f;
}


[Serializable, NetSerializable]
public enum ATMUiKey
{
    Key
}

public enum ATMVisualLayers : byte
{
    Base,
    BaseUnshaded
}
