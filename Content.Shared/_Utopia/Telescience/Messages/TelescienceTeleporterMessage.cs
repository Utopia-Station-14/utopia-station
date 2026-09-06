using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Telescience.Messages
{
    [Serializable, NetSerializable]
    public sealed class TelescienceSendEvent(Vector2 coordinates) : EntityEventArgs
    {
        public Vector2 Coordinates = coordinates;
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceRetrieveEvent(Vector2 coordinates) : EntityEventArgs
    {
        public Vector2 Coordinates = coordinates;
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceOpenPortalEvent(Vector2 coordinates) : EntityEventArgs
    {
        public Vector2 Coordinates = coordinates;
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceClosePortalEvent : EntityEventArgs;

    [Serializable, NetSerializable]
    public sealed class TelescienceCooldownEvent(TimeSpan time) : EntityEventArgs
    {
        public TimeSpan Cooldown = time;
    }
}
