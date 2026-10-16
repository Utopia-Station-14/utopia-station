using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Shared._Utopia.Audio.Events;

namespace Content.Shared._Utopia.Audio.Systems;

public abstract partial class NowPlayingSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<NowPlayingMessage>(OnNowPlaying);
    }

    public void NotifyNowPlaying(EntityUid source, ResolvedSoundSpecifier sound, float radius)
    {
        if (ResolvedSoundSpecifier.IsNullOrEmpty(sound))
            return;

        var coords = _transform.GetMapCoordinates(source);
        var filter = Filter.Empty().AddInRange(coords, radius);

        RaiseNetworkEvent(new NowPlayingMessage(sound), filter);
    }

    public void NotifyNowPlaying(EntityUid source, SoundSpecifier sound, float radius)
    {
        NotifyNowPlaying(source, _audio.ResolveSound(sound), radius);
    }

    protected virtual void OnNowPlaying(NowPlayingMessage msg)
    {
        // client-sided lmao
    }
}
