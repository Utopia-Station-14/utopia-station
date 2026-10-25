using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Sponsors;

[Serializable, NetSerializable]
public sealed class CheckUserSponsor(string p) : EntityEventArgs
{
    public string Player = p;
}

[Serializable, NetSerializable]
public sealed class CheckedUserSponsor(bool p) : EntityEventArgs
{
    public bool IsSponsor = p;
}
