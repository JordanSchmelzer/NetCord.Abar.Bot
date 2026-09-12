using Lavalink4NET;
using Lavalink4NET.NetCord;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;

using Microsoft.EntityFrameworkCore;
using NetCord.Abar.Bot.Database.Models;
using NetCord.Abar.Bot.Services.Interfaces;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Runtime.CompilerServices;

namespace NetCord.Abar.Bot.Commands;


public class AbarCore: ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly SoundDbContext _db;
    private readonly IVoiceService _voiceService;
    private readonly IAudioService _audioService;

    public AbarCore(
        SoundDbContext db, 
        IVoiceService voiceService, 
        IAudioService audioService)
    {
        _db = db;
        _voiceService = voiceService;
        _audioService = audioService;
    }


    static T CreateMessage<T>() where T : IMessageProperties, new()
    {
        return new()
        {
            Content = "Hello, World!",
            Components = [],
        };
    }


    // === Audio Player ===
    private async ValueTask<QueuedLavalinkPlayer?> GetPlayerAsync(bool connectToVoiceChannel = true)
    {
        // Specify the behavior of th eplayer when the user is not connected to a voice channel.
        // if true, the player will join teh voice channel
        // if false, the the player willl not join voice and method returns null on player fetch
        var channelBehavior = connectToVoiceChannel
            ? PlayerChannelBehavior.Join
            : PlayerChannelBehavior.None;

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: channelBehavior);

        /*
         * We use an extension method to create a player. The method will check if a player 
         * already exists for the guild and return it. If no player exists, it will create a 
         * new player and return it. We specify the player factory to use. In this case, we 
         * want to use the QueuedLavalinkPlayer. The QueuedLavalinkPlayer is a player that allows 
         * you to queue tracks and play them one after another.
         */
        var result = await _audioService.Players
            .RetrieveAsync(Context, playerFactory: PlayerFactory.Queued, retrieveOptions)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            var errorMessage = result.Status switch
            {
                // The user is not connected to a voice channel. We can't play music if the user is not
                PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",

                // The bot is not connected to a voice channel. We can't play music if the bot is not
                PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",

                // Something else happened. Later there are some features which may return additional
                // retrieve states, you can add an error message for these states here.
                // All error messages are managed in this method, so you can easily change them 
                // later and do not have to type them over and over again
                _ => "Unknown error.",
            };

            // Send the error message to the user.
            await FollowupAsync(errorMessage).ConfigureAwait(false);

            // Return null to indicate that no player was retrieved.
            return null;
        }

        // PlayerRetrieveResult also contains PlayerRetrieveStatus which indicates what happened when the
        // player was retrieved.
        return result.Player;
    }


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


    [SlashCommand("play", "play from lavalink")]
    public async Task PlayAsync(string query)
    {
        // Immediately tell Discord to wait (Stops did not respond err if takes too long)
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        // Retrieve the player using the method we created earlier.
        // We allow to connect to the voice channel if the user is not connected.
        var player = await GetPlayerAsync(connectToVoiceChannel: true).ConfigureAwait(false);

        // If the player is null, something failed. We already sent an error message to the user
        if (player is null)
        {
            return;
        }

        // Load the track from YouTube. This may take some time, so we await the result.
        string audioFilePath = @"C:/Users/jorda/source/repos/NetCord.Abar.Bot/AbarBot/Sounds/GLORB - EUGENE (Official Music Video).mp3";
        FileInfo audioFileInfo = new(audioFilePath);
        // If no track was found, we send an error message to the user.
        if (!audioFileInfo.Exists)
        {
            return;
        }

        // Play the track and inform the user about the track that is being played.
        await player.PlayFileAsync(
            fileInfo: audioFileInfo,
            enqueue: true,
            properties: default,
            cancellationToken: default
        );
        Console.WriteLine("played thing");
    }

    
    [SlashCommand("stop", "stop the current playing audio in this channel")]
    public async Task StopAsync()
    {
        // Immediately tell Discord to wait (Stops did not respond err if takes too long)
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        // Retrieve the player using the method we created earlier.
        // We do not allow to connect to the voice channel if the user is not connected.
        // It would not make sense to connect the player to the voice channel, only to stop it.
        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties()
            {
                Content = "I didn't find a player to stop in this channel. I did nothing!",
                Flags = MessageFlags.Ephemeral
            });
            await Task.Delay(10000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        // Check if the player is playing
        // TODO: Verify its not playing better. This works for now.
        if (player.CurrentItem is null)
        {
            // If the player is not playing, we send an error message to the user
            await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties()
            {
                Content = "The channel player isn't playing right now. I did nothing!",
                Flags = MessageFlags.Ephemeral // Optional: Makes the error only visible to the user
            });
            await Task.Delay(10000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        // Stop the player and send a message to the user
        await player.StopAsync().ConfigureAwait(false);
        await Context.Interaction.SendFollowupMessageAsync(new InteractionMessageProperties()
        {
            Content = "I stopped the player for you.",
            Flags = MessageFlags.Ephemeral // Optional: Makes the error only visible to the user
        });
        await Task.Delay(10000);
        await Context.Interaction.DeleteResponseAsync();
        return;
    }


    [SlashCommand("pause", "pauses the current playing audio track")]
    public async Task PauseAsync()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        // let the caller know theres no player in this channel
        if (player is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "I couldn't find a player in this channel.",
                Flags = MessageFlags.Ephemeral
            });
            await Task.Delay(10000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        // let the caller know the player is already paused
        if (player.State is PlayerState.Paused)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "Player is already paused.",
                Flags = MessageFlags.Ephemeral
            });
            await Task.Delay(10000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }
        
        // Pause the player
        await player.PauseAsync().ConfigureAwait(false);

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = "Paused",
            Flags = MessageFlags.Ephemeral
        });
        await Task.Delay(10000);
        await Context.Interaction.DeleteResponseAsync();
        return;
    }


    [SlashCommand("resume", "resumes the paused audio track if any")]
    public async Task ResumeAsync()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "I couldn't find a player in this channel.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        // Player must be paused
        if (player.State is not PlayerState.Paused)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "The player is not paused.",
                Flags = MessageFlags.Ephemeral
            });
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
        int volume = 100
    )
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
        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);
        if (player is null)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new()
            {
                Content = "I didn't find a player in this channel.",
                Flags = MessageFlags.Ephemeral
            }));
            await Task.Delay(10000);
            await Context.Interaction.DeleteResponseAsync();
            return;
        }

        // Set the volume
        await player.SetVolumeAsync(volume / 100f).ConfigureAwait(false);
    }


    [SlashCommand("position", "Shows the track position")]
    public async Task PositionAsync()
    {
        // Acknowledge the interaction immediately
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var player = await GetPlayerAsync(connectToVoiceChannel: false).ConfigureAwait(false);

        if (player is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "I couldn't find a player in this channel.",
                Flags = MessageFlags.Ephemeral
            });
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

        var player = await GetPlayerAsync(connectToVoiceChannel: false);

        if (player is null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "I couldn't find a player in this channel.",
                Flags = MessageFlags.Ephemeral
            });
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
    // === ===  
}