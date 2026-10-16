using Robust.Shared.Serialization;
using Robust.Shared.Audio;

namespace Content.Shared._Utopia.Audio.Events;

[Serializable, NetSerializable]
public sealed class NowPlayingMessage : EntityEventArgs
{
    public readonly ResolvedSoundSpecifier Sound;

    public NowPlayingMessage(ResolvedSoundSpecifier sound)
    {
        Sound = sound;
    }
}