using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Genetics;

[Serializable, NetSerializable]
public sealed partial class DNASequenceInjectorDoAfterEvent : SimpleDoAfterEvent;
