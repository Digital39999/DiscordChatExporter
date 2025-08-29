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

[Command("exportguild", Description = "Exports all channels within the specified server.")]
public class ExportGuildCommand : ExportCommandBase
{
    [CommandOption("guild", 'g', Description = "Server ID.")]
    public required Snowflake GuildId { get; init; }

    [CommandOption("include-vc", Description = "Include voice channels.")]
    public bool IncludeVoiceChannels { get; init; } = true;

    [CommandOption("ignore-channels", Description = "Channel IDs to ignore (comma-separated).")]
    public IReadOnlyList<Snowflake> IgnoredChannelIds { get; init; } = [];

    [CommandOption("ignore-categories", Description = "Category IDs to ignore (comma-separated).")]
    public IReadOnlyList<Snowflake> IgnoredCategoryIds { get; init; } = [];

    [CommandOption(
        "only-channels",
        Description = "Only export these channel IDs (comma-separated). Overrides ignore options."
    )]
    public IReadOnlyList<Snowflake> OnlyChannelIds { get; init; } = [];

    [CommandOption(
        "only-categories",
        Description = "Only export channels from these category IDs (comma-separated). Overrides ignore options."
    )]
    public IReadOnlyList<Snowflake> OnlyCategoryIds { get; init; } = [];

    private bool ShouldIgnoreChannel(Channel channel)
    {
        // If "only" filters are specified, use them exclusively
        if (OnlyChannelIds.Count > 0)
        {
            return !OnlyChannelIds.Contains(channel.Id);
        }

        if (OnlyCategoryIds.Count > 0)
        {
            return channel.Parent == null || !OnlyCategoryIds.Contains(channel.Parent.Id);
        }

        // Check ignored channel IDs
        if (IgnoredChannelIds.Contains(channel.Id))
        {
            return true;
        }

        // Check ignored categories
        if (channel.Parent != null && IgnoredCategoryIds.Contains(channel.Parent.Id))
        {
            return true;
        }

        return false;
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        await base.ExecuteAsync(console);
        var cancellationToken = console.RegisterCancellationHandler();

        var channels = new List<Channel>();
        var ignoredChannels = new List<Channel>();

        await console.Output.WriteLineAsync("Fetching channels...");
        var fetchedChannelsCount = 0;

        await console
            .CreateStatusTicker()
            .StartAsync(
                "...",
                async ctx =>
                {
                    await foreach (
                        var channel in Discord.GetGuildChannelsAsync(GuildId, cancellationToken)
                    )
                    {
                        if (channel.IsCategory)
                            continue;

                        if (!IncludeVoiceChannels && channel.IsVoice)
                            continue;

                        if (ShouldIgnoreChannel(channel))
                        {
                            ignoredChannels.Add(channel);
                            ctx.Status(
                                Markup.Escape($"Ignored '{channel.GetHierarchicalName()}'.")
                            );
                            continue;
                        }

                        channels.Add(channel);
                        ctx.Status(Markup.Escape($"Fetched '{channel.GetHierarchicalName()}'."));
                        fetchedChannelsCount++;
                    }
                }
            );

        await console.Output.WriteLineAsync($"Fetched {fetchedChannelsCount} channel(s).");

        if (ignoredChannels.Count > 0)
        {
            await console.Output.WriteLineAsync(
                $"Ignored {ignoredChannels.Count} channel(s) based on filters."
            );
        }

        await ExportAsync(console, channels);
    }
}
