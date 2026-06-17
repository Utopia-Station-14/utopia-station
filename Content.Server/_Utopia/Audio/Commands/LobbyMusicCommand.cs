using System.IO;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Audio;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Utopia.Audio.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class LobbyMusicCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ContentAudioSystem _audio = default!;

    public override string Command => "lobbymusic";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0])
        {
            case "play":
                if (args.Length < 2)
                    return;

                if (!_audio.TrySetLobbyTrack(args[1], out var matchedTrack))
                {
                    shell.WriteLine(Loc.GetString("cmd-lobbymusic-play-not-found", ("query", args[1])));
                    return;
                }

                shell.WriteLine(Loc.GetString("cmd-lobbymusic-play-set", ("track", matchedTrack!)));
                break;

            case "next":
                if (!_audio.SkipLobbyTrack())
                    return;

                shell.WriteLine(Loc.GetString("cmd-lobbymusic-next-skipped"));
                break;

            default:
                shell.WriteLine(Help);
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(new[] { "play", "next" });

        if (args.Length == 2 && args[0] == "play")
        {
            var tracks = _audio.GetLobbyMusicTracks();
            return CompletionResult.FromOptions(tracks.ToArray());
        }

        return CompletionResult.Empty;
    }
}