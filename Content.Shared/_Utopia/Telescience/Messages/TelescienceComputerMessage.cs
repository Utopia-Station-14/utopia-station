using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Telescience.Messages
{
    [Serializable, NetSerializable]
    public enum TelescienceUiKey
    {
        Key,
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceSendMessage(Vector2 coordinates) : BoundUserInterfaceMessage
    {
        public Vector2 Coordinates = coordinates;
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceRetrieveMessage(Vector2 coordinates) : BoundUserInterfaceMessage
    {
        public Vector2 Coordinates = coordinates;
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceOpenPortalMessage(Vector2 coordinates) : BoundUserInterfaceMessage
    {
        public Vector2 Coordinates = coordinates;
    }

    [Serializable, NetSerializable]
    public sealed class TelescienceClosePortalMessage : BoundUserInterfaceMessage;

    [Serializable, NetSerializable]
    public sealed class TelesciencePositionMessage(Vector2 coordinates) : BoundUserInterfaceMessage
    {
        public Vector2 Coordinates = coordinates;
    }
}
