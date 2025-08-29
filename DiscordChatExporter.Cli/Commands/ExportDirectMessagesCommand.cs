using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using DiscordChatExporter.Cli.Commands.Base;
using DiscordChatExporter.Cli.Utils.Extensions;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using Spectre.Console;

namespace DiscordChatExporter.Cli.Commands;

[Command("exportdm", Description = "Exports all direct message channels.")]
public class ExportDirectMessagesCommand : ExportCommandBase
{
    [CommandOption("ignore-channels", Description = "DM channel IDs to ignore (comma-separated).")]
    public IReadOnlyList<Snowflake> IgnoredChannelIds { get; init; } = [];

    [CommandOption(
        "only-channels",
        Description = "Only export these DM channel IDs (comma-separated). Overrides ignore options."
    )]
    public IReadOnlyList<Snowflake> OnlyChannelIds { get; init; } = [];

    [CommandOption("include-group-dm", Description = "Include group DM channels.")]
    public bool IncludeGroupDMs { get; init; } = true;

    private bool ShouldIgnoreChannel(Channel channel)
    {
        // If "only" filter is specified, use it exclusively
        if (OnlyChannelIds.Count > 0)
            return !OnlyChannelIds.Contains(channel.Id);

        // Check ignored channel IDs
        if (IgnoredChannelIds.Contains(channel.Id))
            return true;

        // Exclude group DMs if flag is false
        if (!IncludeGroupDMs && channel.Kind == ChannelKind.DirectGroupTextChat)
            return true;

        return false;
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);
        var cancellationToken = console.RegisterCancellationHandler();

        var channels = new List<Channel>();
        var ignoredChannels = new List<Channel>();
        var processedChannelsCount = 0;

        await console.Output.WriteLineAsync("Fetching DM channels...");

        await console
            .CreateStatusTicker()
            .StartAsync(
                "...",
                async ctx =>
                {
                    await foreach (
                        var channel in Discord.GetGuildChannelsAsync(
                            Guild.DirectMessages.Id,
                            cancellationToken
                        )
                    )
                    {
                        ctx.Status(
                            Markup.Escape($"Processing '{channel.GetHierarchicalName()}'...")
                        );

                        if (ShouldIgnoreChannel(channel))
                        {
                            ignoredChannels.Add(channel);
                            ctx.Status(
                                Markup.Escape($"Ignored '{channel.GetHierarchicalName()}'.")
                            );
                            continue;
                        }

                        channels.Add(channel);
                        ctx.Status(
                            Markup.Escape($"Added '{channel.GetHierarchicalName()}' for export.")
                        );
                        processedChannelsCount++;
                    }
                }
            );

        await console.Output.WriteLineAsync(
            $"Selected {processedChannelsCount} DM channel(s) for export."
        );

        if (ignoredChannels.Count > 0)
        {
            await console.Output.WriteLineAsync(
                $"Ignored {ignoredChannels.Count} DM channel(s) based on filters."
            );
        }

        await ExportAsync(console, channels);
    }
}
