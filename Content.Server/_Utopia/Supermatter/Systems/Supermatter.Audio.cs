using Content.Shared._Utopia.Supermatter.Components;
using Robust.Shared.Audio.Systems;
using Content.Server._Utopia.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Random;

namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private NowPlayingServerSystem _nowPlay = default!;

    public void PlayAudio(Entity<SupermatterComponent> sm, SoundSpecifier sound, bool global, bool notify)
    {
        if (!global)
        {
            _audio.PlayPvs(sound, sm);
            if (notify)
                _nowPlay.NotifyNowPlaying(sm, sound, 500f);
        }
        else
            _audio.PlayGlobal(sound, sm);
    }

    private void ProcessAudio(Entity<SupermatterComponent> sm, SoundSpecifier sound)
    {
        HandleAccent(sm);
    }

    private void HandleAccent(Entity<SupermatterComponent> sm)
    {
        if (sm.Comp.AccentLastTime >= _timing.CurTime || !_random.Prob(0.05f))
            return;

        var aggression = Math.Min((sm.Comp.CurrentDamage / 800) * (sm.Comp.InternalEnergy / 2500), 1) * 100;
        var nextSound = Math.Max(Math.Round((100 - aggression) * 5), AccentMinCooldown);
        var sound = CalmAccent;

        if (sm.Comp.AccentLastTime + TimeSpan.FromSeconds(nextSound) > _timing.CurTime)
            return;

        if (sm.Comp.Status >= SupermatterStatus.Destabilization)
            sound = DelamAccent;

        PlayAudio(sm, sound, false, false);
        sm.Comp.AccentLastTime = _timing.CurTime;
    }
}
