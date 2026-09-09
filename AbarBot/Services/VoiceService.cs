using System.Collections.Concurrent;

using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

using NetCord.Abar.Bot.Services.Interfaces;

using NetCord.Abar.Bot.Models;

namespace NetCord.Abar.Bot.Services;


public class VoiceService : IVoiceService
{
    // Dictionary to hold voice instances per guild
    private static readonly ConcurrentDictionary<ulong, VoiceInstance?> _voiceInstance = new();

    // Join a voice channel
    public async Task<InteractionMessageProperties> JoinAsync(
        ApplicationCommandContext ctx,
        IVoiceGuildChannel? channel = null)
    {
        // Ensure guild exists
        if (ctx.Guild is not { } guild)
        {
            return new InteractionMessageProperties()
                .WithContent("The guild is not available. Try again later.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        var user = ctx.User;

        // Determine channel ID
        ulong channelId;
        if (channel is not null)
        {
            channelId = channel.Id;
        }
        else if (guild.VoiceStates.TryGetValue(user.Id, out var voiceState))
        {
            channelId = voiceState.ChannelId.GetValueOrDefault();
        }
        else
        {
            return new InteractionMessageProperties()
                .WithContent("You must specify a channel or be connected to a voice channel.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        var guildId = guild.Id;

        // Prevent multiple connections in same guild
        if (!_voiceInstance.TryAdd(guildId, null))
        {
            return new InteractionMessageProperties()
                .WithContent("Already connected to a voice channel in this guild.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        VoiceClient? voiceClient;

        try
        {
            voiceClient = await ctx.Client.JoinVoiceChannelAsync(
                guildId,
                channelId,
                new VoiceClientConfiguration
                {
                    Logger = new ConsoleLogger(),
                });
        }
        catch
        {
            _voiceInstance.TryRemove(new(guildId, null));
            await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));
            throw;
        }

        // Wrap in VoiceInstance
        VoiceInstance voiceInstance = new(voiceClient);

        if (!_voiceInstance.TryUpdate(guildId, voiceInstance, null))
        {
            // Should never happen, but safe cleanup
            voiceInstance.Dispose();
            await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));

            return new InteractionMessageProperties()
                .WithContent("Failed to register voice connection.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        // Disconnect handler
        voiceClient.Disconnect += args =>
        {
            if (args.Reconnect)
                return default;

            if (_voiceInstance.TryRemove(new(guildId, voiceInstance)))
                voiceInstance.Dispose();

            return default;
        };

        try
        {
            await voiceClient.StartAsync();
        }
        catch
        {
            if (_voiceInstance.TryRemove(new(guildId, voiceInstance)))
            {
                voiceInstance.Dispose();
                await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));
            }

            throw;
        }

        return "Joined voice channel.";
    }

    // Leave a voice channel
    public async Task<InteractionMessageProperties> LeaveAsync(
        ApplicationCommandContext ctx)
    {
        if (ctx.Guild is not { } guild)
        {
            return new InteractionMessageProperties()
                .WithContent("The guild is not available. Try again later.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        var guildId = guild.Id;

        // Check if we have a voice instance for this guild
        if (!_voiceInstance.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
        {
            return new InteractionMessageProperties()
                .WithContent("Not connected to a voice channel in this guild.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        // Remove instance from dictionary
        if (_voiceInstance.TryRemove(new(guildId, voiceInstance)))
        {
            // Dispose the instance (this disposes the VoiceClient)
            voiceInstance.Dispose();

            // Tell Discord we are leaving voice
            await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));

            return "Left the voice channel.";
        }

        // If removal failed (rare), still attempt cleanup
        voiceInstance.Dispose();
        await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));

        return new InteractionMessageProperties()
            .WithContent("Failed to unregister voice connection, but disconnected anyway.")
            .WithFlags(MessageFlags.Ephemeral);
    }
}
