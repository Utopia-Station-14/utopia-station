using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.PDA.PdaPainter;

[Serializable, NetSerializable]
public enum PdaPainterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class PdaPainterBoundUserInterfaceState(string? pdaName, string? cardName, HashSet<ProtoId<EntityPrototype>> pdaPrototypes, HashSet<ProtoId<EntityPrototype>> cardPrototypes) : BoundUserInterfaceState
{
    public readonly string? PdaName = pdaName;
    public readonly string? CardName = cardName;
    public readonly HashSet<ProtoId<EntityPrototype>> PdaPresets = pdaPrototypes;
    public readonly HashSet<ProtoId<EntityPrototype>> CardPresets = cardPrototypes;
}

[Serializable, NetSerializable]
public sealed class PdaPainterPaintPdaMessage(string protoId) : BoundUserInterfaceMessage
{
    public readonly string ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class PdaPainterApplyCardMessage(string protoId) : BoundUserInterfaceMessage
{
    public readonly string ProtoId = protoId;
}

[Serializable, NetSerializable]
public sealed class PdaPainterEjectMessage(bool pda) : BoundUserInterfaceMessage
{
    public readonly bool Pda = pda;
}

[Serializable, NetSerializable]
public enum PdaPainterVisuals : byte
{
    IdCardPrototype,
    PdaPrototype
}
