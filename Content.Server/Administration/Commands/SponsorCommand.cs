using System.Linq;
using Content.Server.Database;
using Content.Server._Utopia.Sponsors;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SponsorCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IServerDbManager _dbManager = default!;
    [Dependency] private SponsorsManager _sponsorsManager = default!;

    public override string Command => "sponsor";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1)
        {
            shell.WriteLine("Usage: sponsor list | sponsor create <player> <tier> <days> <oocColor> <priorityJoin> <extraSlots> <allowJob> <allowedMarkings> | sponsor delete <player> | sponsor extend <player> <days>");
            return;
        }

        var action = args[0].ToLowerInvariant();

        if (action == "list")
        {
            if (args.Length != 1)
            {
                shell.WriteError("Usage: sponsor list");
                return;
            }

            var sponsors = await _dbManager.GetSponsorList() ?? [];
            foreach (var sponsor in sponsors)
            {
                var sponsorPlayer = await _playerLocator.LookupIdAsync(new NetUserId(sponsor.UserId));
                var playerName = sponsorPlayer?.Username ?? sponsor.UserId.ToString();
                shell.WriteLine($"{playerName} | tier: {sponsor.Tier} | expires: {sponsor.ExpireDate:yyyy-MM-dd}");
            }

            return;
        }

        if (args.Length < 2)
        {
            shell.WriteLine("Usage: sponsor create <player> <tier> <days> <oocColor> <priorityJoin> <extraSlots> <allowJob> | sponsor delete <player> | sponsor extend <player> <days>");
            return;
        }

        var player = await _playerLocator.LookupIdByNameOrIdAsync(args[1]);
        if (player == null)
        {
            shell.WriteError($"Player '{args[1]}' was not found.");
            return;
        }

        switch (action)
        {
            case "create":
            {
                if (args.Length != 9 ||
                    !int.TryParse(args[2], out var tier) || tier < 1 ||
                    !TryGetDays(args[3], out var days) ||
                    !Color.TryFromHex(args[4], out _) ||
                    !bool.TryParse(args[5], out var havePriorityJoin) ||
                    !int.TryParse(args[6], out var extraSlots) || extraSlots < 0 ||
                    !bool.TryParse(args[7], out var allowJob))
                {
                    shell.WriteError("Usage: sponsor create <player> <tier> <days> <oocColor> <priorityJoin> <extraSlots> <allowJob> <allowedMarkings>");
                    return;
                }

                var expireDate = DateTime.UtcNow.AddDays(days);
                var added = await _dbManager.AddSponsor(new Sponsor
                {
                    UserId = player.UserId.UserId,
                    Tier = tier,
                    ExpireDate = expireDate,
                    OOCColor = args[4],
                    HavePriorityJoin = havePriorityJoin,
                    ExtraSlots = extraSlots,
                    AllowJob = allowJob,
                    AllowedMarkings = args[8],
                });

                if (!added)
                {
                    shell.WriteError($"Player '{player.Username}' already has a sponsor record.");
                    return;
                }

                await _sponsorsManager.RefreshSponsorInfo(player.UserId);
                shell.WriteLine($"Created sponsor for {player.Username} until {expireDate:yyyy-MM-dd}.");
                break;
            }
            case "delete":
            {
                if (args.Length != 2)
                {
                    shell.WriteError("Usage: sponsor delete <player>");
                    return;
                }

                var removed = await _dbManager.RemoveSponsor(player.UserId);
                if (!removed)
                {
                    shell.WriteError($"Player '{player.Username}' has no sponsor record.");
                    return;
                }

                await _sponsorsManager.RefreshSponsorInfo(player.UserId);
                shell.WriteLine($"Deleted sponsor for {player.Username}.");
                break;
            }
            case "extend":
            {
                if (args.Length != 3 || !TryGetDays(args[2], out var extensionDays))
                {
                    shell.WriteError("Usage: sponsor extend <player> <days> (days: 1-36500)");
                    return;
                }

                var sponsor = await _dbManager.GetSponsorInfo(player.UserId);
                if (sponsor == null)
                {
                    shell.WriteError($"Player '{player.Username}' has no sponsor record.");
                    return;
                }

                var now = DateTime.UtcNow;
                var expireDate = (sponsor.ExpireDate > now ? sponsor.ExpireDate : now).AddDays(extensionDays);
                if (!await _dbManager.SetSponsorExpireDate(player.UserId, expireDate))
                {
                    shell.WriteError($"Player '{player.Username}' has no sponsor record.");
                    return;
                }

                await _sponsorsManager.RefreshSponsorInfo(player.UserId);
                shell.WriteLine($"Extended sponsor subscription for {player.Username} until {expireDate:yyyy-MM-dd}.");
                break;
            }
            default:
                shell.WriteError("Unknown action. Use create, delete, or extend.");
                break;
        }
    }

    private static bool TryGetDays(string value, out int days)
    {
        return int.TryParse(value, out days) && days is >= 1 and <= 36500;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(["list", "create", "delete", "extend"]);

        if (args.Length == 2 && args[0] is "create" or "delete" or "extend")
        {
            var players = _playerManager.Sessions.Select(session => session.Name).OrderBy(name => name);
            return CompletionResult.FromHintOptions(players, Loc.GetString("cmd-sponsor-hint-player"));
        }

        if (args.Length == 3 && args[0] == "create")
            return CompletionResult.FromHint(Loc.GetString("cmd-sponsor-hint-tier"));

        if (args.Length == 3 && args[0] == "extend")
            return CompletionResult.FromHint(Loc.GetString("cmd-sponsor-hint-days"));

        if (args.Length == 4 && args[0] == "create")
            return CompletionResult.FromHint(Loc.GetString("cmd-sponsor-hint-days"));

        if (args.Length == 5 && args[0] == "create")
            return CompletionResult.FromHint(Loc.GetString("cmd-sponsor-hint-color"));

        if (args.Length == 6 && args[0] == "create")
            return CompletionResult.FromHintOptions(["true", "false"], Loc.GetString("cmd-sponsor-hint-priority-join"));

        if (args.Length == 7 && args[0] == "create")
            return CompletionResult.FromHint(Loc.GetString("cmd-sponsor-hint-extra-slots"));

        if (args.Length == 8 && args[0] == "create")
            return CompletionResult.FromHintOptions(["true", "false"], Loc.GetString("cmd-sponsor-hint-allow-job"));

        if (args.Length == 9 && args[0] == "create")
            return CompletionResult.FromHint(Loc.GetString("cmd-sponsor-hint-allowed-markings"));

        return CompletionResult.Empty;
    }
}
