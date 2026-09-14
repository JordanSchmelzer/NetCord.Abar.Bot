using Lavalink4NET;
using Lavalink4NET.Clients;
using Lavalink4NET.NetCord;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Preconditions;
using Lavalink4NET.Players.Queued;

using NetCord.Abar.Bot.Services.Interfaces;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Collections.Immutable;

namespace NetCord.Abar.Bot.Commands;


public class MusicController: ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly IAudioService _audioService;
    private readonly ITrackSearchService _trackSearchService;

    public MusicController(
        IAudioService audioService,
        ITrackSearchService trackSearchService)
    {
        _audioService = audioService;
        _trackSearchService = trackSearchService;
    }


    private static EmbedProperties CreateErrorEmbed(PlayerResult<QueuedLavalinkPlayer> result)
    {
        var title = result.Status switch
        {
            PlayerRetrieveStatus.UserNotInVoiceChannel => "You must be in a voice channel.",
            PlayerRetrieveStatus.BotNotConnected => "The bot is not connected to any channel.",
            PlayerRetrieveStatus.VoiceChannelMismatch => "You must be in the same voice channel as the bot.",

            PlayerRetrieveStatus.PreconditionFailed when result.Precondition == PlayerPrecondition.Playing => "The player is currently not playing any track.",
            PlayerRetrieveStatus.PreconditionFailed when result.Precondition == PlayerPrecondition.NotPaused => "The player is already paused.",
            PlayerRetrieveStatus.PreconditionFailed when result.Precondition == PlayerPrecondition.Paused => "The player is not paused.",
            PlayerRetrieveStatus.PreconditionFailed when result.Precondition == PlayerPrecondition.QueueEmpty => "The queue is empty.",
            PlayerRetrieveStatus.PreconditionFailed when result.Precondition == PlayerPrecondition.NotPlaying => "The player is playing.",

            _ => "Unknown error.",
        };

        return new EmbedProperties
        {
            Title = title,
            Color = new Color(255, 50, 50)
        };
    }


    // === Audio Player ===
    /// <summary>
    /// Tries to retrieve a Lavalink player for the current guild. If no player exists, it will create one.
    /// </summary>
    private async ValueTask<QueuedLavalinkPlayer?> TryGetPlayerAsync(
        bool allowConnect = false,
        bool requireChannel = true,
        ImmutableArray<IPlayerPrecondition> preconditions = default,
        bool isDeferred = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = new PlayerRetrieveOptions(
            ChannelBehavior: allowConnect ? PlayerChannelBehavior.Join : PlayerChannelBehavior.None,
            VoiceStateBehavior: requireChannel ? MemberVoiceStateBehavior.RequireSame : MemberVoiceStateBehavior.Ignore,
            Preconditions: preconditions);

        /*
         * We use an extension method to create a player. The method will check if a player 
         * already exists for the guild and return it. If no player exists, it will create a 
         * new player and return it. We specify the player factory to use. In this case, we 
         * want to use the QueuedLavalinkPlayer. The QueuedLavalinkPlayer is a player that allows 
         * you to queue tracks and play them one after another.
         */
        var result = await _audioService.Players
            .RetrieveAsync(Context, playerFactory: PlayerFactory.Queued, options, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return result.Player;
        }

        // See the error handling section for more information
        var errorMessage = CreateErrorEmbed(result);

        if (isDeferred)
        {
            await FollowupAsync(new() { Embeds = [errorMessage] }).ConfigureAwait(false);
        }
        else
        {
            await RespondAsync(
                InteractionCallback.Message(
                    new InteractionMessageProperties
                    {
                        Embeds = [errorMessage],
                        Flags = MessageFlags.Ephemeral
                    }
                )
            ).ConfigureAwait(false);
        }

        return null;
    }


    // Ping Test
    [SlashCommand("ping", "respond to a slash command")]
    public async Task PingPongAsync()
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
        {
            Content = "Pong",
            Flags = MessageFlags.Ephemeral 
        }));
        await Task.Delay(10000);
        await Context.Interaction.DeleteResponseAsync();
        return;
    }


    [SlashCommand("playrandom", "Plays (and queues) a random track.")]
    public async Task PlayRandomAsync()
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await TryGetPlayerAsync(
            allowConnect: true,
            isDeferred: true)
                .ConfigureAwait(false);

        if (player is null)
            return;

        var files = _trackSearchService.GetAudioFiles()
            .ToList();

        if (files.Count == 0)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "No audio files found.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        var randomFile = files[new Random().Next(files.Count)];
        var fileInfo = new FileInfo(randomFile);

        await player.PlayFileAsync(
            fileInfo,
            enqueue: true,
            properties: default,
            cancellationToken: default);

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Randomly selected: **{fileInfo.Name}**",
            Flags = MessageFlags.Ephemeral
        });
    }


    // Basic Controls
    [SlashCommand("play", "play from lavalink")]
    public async Task PlayAsync(string query)
    {
        // Immediately tell Discord to wait (Stops did not respond err if takes too long)
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        // Retrieve the player using the method we created earlier.
        // We allow to connect to the voice channel if the user is not connected.
        var player = await TryGetPlayerAsync(
            allowConnect: true, 
            isDeferred: true)
                .ConfigureAwait(false);

        // If the player is null, something failed. We already sent an error message to the user
        if (player is null)
            return;

        // normalize the users string
        query = query.ToLower().Trim();
        if (query == string.Empty)
            return;

        // Get tracks
        var tracks = _trackSearchService.GetAudioFiles().ToList();
        if (tracks.Count == 0)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "No audio files found in the Sounds folder.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        // Find best match
        var bestMatch = _trackSearchService.FindBestMatchingFile(tracks, query);

        if (bestMatch is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"No matching audio file found for '{query}'.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        var track = new FileInfo(bestMatch);

        // Play the track and inform the user about the track that is being played.
        await player.PlayFileAsync(
            fileInfo: track,
            enqueue: true,
            properties: default,
            cancellationToken: default
        );
    }


    [SlashCommand("search", "Searches your Sounds folder for matching audio files")]
    public async Task SearchAsync(string query)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var files = _trackSearchService.GetAudioFiles().ToList();

        var matches = files
            .Where(f => Path.GetFileNameWithoutExtension(f)
                .Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(25)
            .ToList();

        if (matches.Count == 0)
        {
            await FollowupAsync(new InteractionMessageProperties
            {
                Content = $"No files found matching '{query}'.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        string list = string.Join("\n", matches.Select(f => "- " + Path.GetFileName(f)));

        await FollowupAsync(new InteractionMessageProperties
        {
            Content = $"Found {matches.Count} file(s):\n{list}",
            Flags = MessageFlags.Ephemeral
        });
    }


    [SlashCommand("stop", "stop the current playing audio in this channel")]
    public async Task StopAsync()
    {
        // Immediately tell Discord to wait (Stops did not respond err if takes too long)
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        // Retrieve the player using the method we created earlier.
        // We do not allow to connect to the voice channel if the user is not connected.
        // It would not make sense to connect the player to the voice channel, only to stop it.
        var player = await TryGetPlayerAsync(
            allowConnect: false,
            isDeferred: true);

        if (player is null)
        {
            return;
        }

        // Stop the player and send a message to the user
        await player.StopAsync().ConfigureAwait(false);

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = "I stopped the player for you.",
            Flags = MessageFlags.Ephemeral // Optional: Makes the error only visible to the user
        });
        return;
    }


    [SlashCommand("pause", "pauses the current playing audio track")]
    public async Task PauseAsync()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await TryGetPlayerAsync(
            allowConnect: false,
            // pass NotPaused to ensure player is not paused
            preconditions: ImmutableArray.Create(PlayerPrecondition.NotPaused),
            isDeferred: true);

        if (player is null)
        {
            return;
        }
        
        // Pause the player
        await player.PauseAsync().ConfigureAwait(false);

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = "Paused",
            Flags = MessageFlags.Ephemeral
        });
        return;
    }


    [SlashCommand("resume", "resumes the paused audio track if any")]
    public async Task ResumeAsync()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await TryGetPlayerAsync(
            allowConnect: false,
            // pass Paused to ensure player is paused
            preconditions: ImmutableArray.Create(PlayerPrecondition.Paused),
            isDeferred: true);

        if (player is null)
        {
            return;
        }

        // Resume playback
        await player.ResumeAsync().ConfigureAwait(false);

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = "Resumed the track.",
            Flags = MessageFlags.Ephemeral
        });
    }


    [SlashCommand("volume", "Sets the player volume (0 - 1000%)")]
    public async Task VolumeAsync(
        [SlashCommandParameter(Description = "The volume level from 0 to 1000%")]
        int volume = 100)
    {
        // Immediately tell Discord to wait (Stops did not respond err if takes too long)
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        // Must be withing supported range
        if (volume is > 1000 or < 0)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "The volume is out of range: 0% - 1000%!",
                Flags = MessageFlags.Ephemeral
            }));
            await Task.Delay(10000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        // Fetch the player
        var player = await TryGetPlayerAsync(
            allowConnect: false,
            isDeferred: true)
                .ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        // Set the volume
        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
    }


    // Queue Related
    [SlashCommand("position", "Shows the track position")]
    public async Task PositionAsync()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await TryGetPlayerAsync(
            allowConnect: false,
            isDeferred: true)
                .ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentTrack is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "Nothing is playing right now!",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        // Lavalink4NET exposes Position as a TimeSpan? inside a wrapper
        var pos = player.Position?.Position ?? TimeSpan.Zero;
        var dur = player.CurrentTrack.Duration;

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Position: **{pos:mm\\:ss}** / **{dur:mm\\:ss}**",
            Flags = MessageFlags.Ephemeral
        });
    }


    [SlashCommand("skip", "Skips the current track")]
    public async Task Skip()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await TryGetPlayerAsync(
            allowConnect: false,
            isDeferred: true)
                .ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        if (player.CurrentTrack is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "Nothing is playing right now!",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        // Perform the skip
        await player.SkipAsync().ConfigureAwait(false);

        // Check what is now playing
        var nextTrack = player.CurrentTrack;

        if (nextTrack is not null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Skipped. Now playing:\n{nextTrack.Uri}",
                Flags = MessageFlags.Ephemeral
            });
        }
        else
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "Skipped. The queue is now empty, so playback has stopped.",
                Flags = MessageFlags.Ephemeral
            });
        }
    }


    // Seek todo


    [SlashCommand("shuffle", "set the player shuffle mode on or off")]
    public async Task SetShuffleAsync(
        [SlashCommandParameter(Description = "'on' or 'off'. Set to On to play a random track in queue.")]
        string mode)
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await TryGetPlayerAsync(
            allowConnect: false,
            isDeferred: true)
                .ConfigureAwait(false);

        if (player is null)
        {
            return;
        }

        // Determine shuffle mode
        bool shuffleMode = mode.Equals("on", StringComparison.OrdinalIgnoreCase);

        player.Shuffle = shuffleMode;

        // Send confirmation
        await FollowupAsync(new InteractionMessageProperties
        {
            Content = shuffleMode
                ? "Shuffle mode is now **ON**. Tracks will play in random order."
                : "Shuffle mode is now **OFF**. Tracks will play in queue order.",
            Flags = MessageFlags.Ephemeral
        })
        .ConfigureAwait(false);
    }
    // === ===  
}