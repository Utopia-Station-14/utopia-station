using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Utopia.PDA.PdaPainter;

[RegisterComponent, NetworkedComponent]
public sealed partial class PdaPainterComponent : Component
{
    [DataField]
    public ItemSlot PdaSlot = new();

    [DataField]
    public ItemSlot CardSlot = new();

    [DataField]
    public SoundSpecifier PaintSound = new SoundPathSpecifier("/Audio/Effects/Fluids/vacuum-cleaner-fast.ogg");

    public const string PdaSlotId = "pdaSlot";
    public const string CardSlotId = "cardSlot";
    public const string WhitelistTag = "UtopiaPdaPainterWhitelist";
    public HashSet<ProtoId<EntityPrototype>> AllowedIdCardPrototypes = [];
    public HashSet<ProtoId<EntityPrototype>> AllowedPdaPrototypes = [];
}
