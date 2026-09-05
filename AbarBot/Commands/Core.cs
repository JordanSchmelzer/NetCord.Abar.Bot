using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Abar.Bot.Database.Models;
using NetCord.Gateway.Voice;
using NetCord.Logging;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Collections.Concurrent;
using System.Diagnostics;


namespace NetCord.Abar.Bot.Commands;


public class AbarCore : ApplicationCommandModule<ApplicationCommandContext>
{
    private static readonly ConcurrentDictionary<ulong, VoiceInstance?> voiceInstances = new();
    private readonly SoundDbContext _db;


    public AbarCore(SoundDbContext db)
    {
        _db = db;
    }


    static T CreateMessage<T>() where T : IMessageProperties, new()
    {
        return new()
        {
            Content = "Hello, World!",
            Components = [],
        };
    }


    [SlashCommand("soundlist", "List all app sounds")]
    public async Task GetAppSoundsAsync()
    {
        var ctx = Context;
        List<Sound> sounds = await _db.Sounds.ToListAsync();

        var embed = new EmbedProperties
        {
            Title = "Available Sounds",
            Description = sounds.Count == 0
                ? "No sounds found."
                : string.Join("\n", sounds.Select(s => $"- {s.SearchTerm}")),
            Color = new Color(0, 255, 180)
        };

        await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
        {
            Embeds = [embed],
        }));
    }


    [SlashCommand("channelinfo", "return embed wiht info about current channel")]
    public async Task GetChannelInfoAsync()
    {
        var ctx = Context;
        EmbedProperties embed;

        embed = new EmbedProperties
        {
            Title = "Channel Info",
            Description = $"ID: {ctx.Channel.Id}\nType:",
            Color = new Color(0, 255, 180)
        };

        await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties
            {
                Embeds = [embed],
            })
        );
    }


    [SlashCommand("ping", "Ping!")]
    public static string Ping() => "Pong!";


    [SlashCommand("join", "Joins the user's voice channel or a specified one")]
    public async Task<InteractionMessageProperties> JoinVoiceAsync(IVoiceGuildChannel? channel = null)
    {
        var ctx = Context;
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
        if (!voiceInstances.TryAdd(guildId, null))
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
            voiceInstances.TryRemove(new(guildId, null));
            await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));
            throw;
        }

        // Wrap in VoiceInstance
        VoiceInstance voiceInstance = new(voiceClient);

        if (!voiceInstances.TryUpdate(guildId, voiceInstance, null))
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

            if (voiceInstances.TryRemove(new(guildId, voiceInstance)))
                voiceInstance.Dispose();

            return default;
        };

        try
        {
            await voiceClient.StartAsync();
        }
        catch
        {
            if (voiceInstances.TryRemove(new(guildId, voiceInstance)))
            {
                voiceInstance.Dispose();
                await ctx.Client.UpdateVoiceStateAsync(new(guildId, null));
            }

            throw;
        }

        return "Joined voice channel.";
    }


    [SlashCommand("leave", "Leaves the current voice channel")]
    public async Task<InteractionMessageProperties> LeaveVoiceAsync()
    {
        var ctx = Context;

        if (ctx.Guild is not { } guild)
        {
            return new InteractionMessageProperties()
                .WithContent("The guild is not available. Try again later.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        var guildId = guild.Id;

        // Check if we have a voice instance for this guild
        if (!voiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
        {
            return new InteractionMessageProperties()
                .WithContent("Not connected to a voice channel in this guild.")
                .WithFlags(MessageFlags.Ephemeral);
        }

        // Remove instance from dictionary
        if (voiceInstances.TryRemove(new(guildId, voiceInstance)))
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


    [SlashCommand("play", "play audio through a connected voice channel")]
    public async Task PlayAsync()
    {
        var ctx = Context;

        if (ctx.Guild is not { } guild)
        {
            await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("The guild is not available. Try again later.")
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        var guildId = guild.Id;

        if (!voiceInstances.TryGetValue(guildId, out var voiceInstance) || voiceInstance is null)
        {
            await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("Not connected to a voice channel in this guild.")
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        using var job = voiceInstance.TryEnterJob(VoiceJobType.Playing);
        if (job is not { CancellationToken: var cancellationToken })
        {
            await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("Already playing audio in this guild.")
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        await ctx.Interaction.SendResponseAsync(InteractionCallback.Message($"Playing..."));

        var voiceClient = voiceInstance.Client;

        await voiceClient.EnterSpeakingStateAsync(new(SpeakingFlags.Microphone));

        using var voiceStream = voiceClient.CreateVoiceStream();
        using OpusEncodeStream opusEncodeStream = new(voiceStream,
                                                      PcmFormat.Float,
                                                      VoiceChannels.Stereo,
                                                      OpusApplication.Audio);

        //const string Input = "https://netcord.dev/sounds/sample.mp3";
        string inputPath = Path.Combine(
            AppContext.BaseDirectory,
            "Sounds",
            "GLORB - EUGENE (Official Music Video).mp3"
        );

        Console.WriteLine("Working Directory: " + Environment.CurrentDirectory);
        Console.WriteLine("Base Directory: " + AppContext.BaseDirectory);

        string soundsDir = Path.Combine(AppContext.BaseDirectory, "Sounds");
        Console.WriteLine("Checking directory: " + soundsDir);
        if (Directory.Exists(soundsDir))
        {
            foreach (var file in Directory.GetFiles(soundsDir))
                Console.WriteLine("Found file: " + file);
        }
        else
        {
            Console.WriteLine("Sounds directory does NOT exist.");
        }

        if (!File.Exists(inputPath))
        {
            var respMsg = new InteractionMessageProperties()
                .WithContent($"File not found: {inputPath}")
                .WithFlags(MessageFlags.Ephemeral);
            await ctx.Interaction.SendFollowupMessageAsync(respMsg);
            return;
        }

        using var ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            ArgumentList =
            {
                "-i", inputPath,
                "-f", BitConverter.IsLittleEndian ? "f32le" : "f32be",
                "-ar", "48000",
                "-ac", "2",
                "pipe:1",
            },
            RedirectStandardOutput = true,
        })!;

        var ffmpegOutput = ffmpeg.StandardOutput.BaseStream;

        try
        {
            await ffmpegOutput.CopyToAsync(opusEncodeStream, cancellationToken);
            await opusEncodeStream.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            ffmpeg.Kill();

            if (ex is not OperationCanceledException and not AggregateException { InnerException: OperationCanceledException })
                throw;
        }
    }
}