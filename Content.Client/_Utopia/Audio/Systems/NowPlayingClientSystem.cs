using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Shared._Utopia.Audio.Systems;
using Content.Shared._Utopia.Audio.Events;
using Content.Client._Utopia.Audio.UI;

namespace Content.Client._Utopia.Audio.Systems;

public sealed class NowPlayingClientSystem : NowPlayingSystem
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    protected override void OnNowPlaying(NowPlayingMessage msg)
    {
        if (!TryGetPath(msg.Sound, out var path))
            return;

        ShowNowPlayingForPath(path);
    }

    public void ShowNowPlayingForPath(ResPath path)
    {
        var resource = _resourceCache.GetResource<AudioResource>(path);
        var stream = resource.AudioStream;

        var title = string.IsNullOrEmpty(stream.Title)
            ? Loc.GetString("now-playing-unknown-title")
            : stream.Title;

        var artist = string.IsNullOrEmpty(stream.Artist)
            ? Loc.GetString("now-playing-unknown-artist")
            : stream.Artist;

        var markup = Loc.GetString("now-playing-text",
            ("songTitle", title),
            ("songArtist", artist));

        ShowNowPlayingPopup(markup);
    }

    private bool TryGetPath(ResolvedSoundSpecifier sound, out ResPath path)
    {
        switch (sound)
        {
            case ResolvedPathSpecifier pathSpec:
                path = pathSpec.Path;
                return true;

            case ResolvedCollectionSpecifier collectionSpec:
                if (collectionSpec.Collection is { } id
                    && _prototype.TryIndex(id, out var proto)
                    && collectionSpec.Index >= 0
                    && collectionSpec.Index < proto.PickFiles.Count)
                {
                    path = proto.PickFiles[collectionSpec.Index];
                    return true;
                }
                break;
        }

        path = default;
        return false;
    }

    private void ShowNowPlayingPopup(string markup)
    {
        var popup = new NowPlayingPopup(markup);
        _uiManager.WindowRoot.AddChild(popup);

        popup.CenterVertically(_uiManager.WindowRoot.Height);
    }
}