using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Grab;

[Serializable, NetSerializable]
public sealed partial class GrabEscapeDoAfterEvent : SimpleDoAfterEvent;
