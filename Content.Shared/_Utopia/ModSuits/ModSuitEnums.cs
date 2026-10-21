using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.ModSuits;

[Serializable, NetSerializable]
public enum ModSuitPart : byte
{
    Helmet,
    Body,
    Gloves,
    Boots,
}

[Serializable, NetSerializable]
public enum ModSuitUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ModSuitStatus
{
    Inactive,
    Active,
    Charging,
    NeedCharging,
    Dead,
    SPRINGTRAPPED,
}
