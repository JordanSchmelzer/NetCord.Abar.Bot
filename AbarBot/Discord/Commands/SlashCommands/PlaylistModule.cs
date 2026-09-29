using Microsoft.Extensions.Logging;

using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

using NetCord.Abar.Bot.Definitions.Models;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Tools.Factories;
using Microsoft.EntityFrameworkCore;


namespace NetCord.Abar.Bot.Discord.Commands.SlashCommands;


[SlashCommand("playlist", "Playlist Commands")]
public class PlaylistModule(
    AbarBotDbContext db,
    ValidationService validationService,
    ResponseService responseService,
    ILogger<PlaylistModule> logger
    ) : ApplicationCommandModule<ApplicationCommandContext>
{
    public static readonly int PlaylistUserLimt = 10;


    [SubSlashCommand("add", "add a new playlist")]
    public async Task AddPlaylistAsync(
        [SlashCommandParameter(Name = "name", Description = "Name of the playlist to add. If none, open a form.")]
        string? name = null)
    {
        try
        {
            await validationService.EnsureUserAsync(Context.User.Id, Context.User.Username);

            if ( string.IsNullOrWhiteSpace(name) )
            {
                // This works if I dont defer the response
                await RespondAsync(InteractionCallback.Modal(ModalFactory.CreateActionSelectModal()));
                return;
            }

            var userId = ( int )Context.Interaction.User.Id;
            var existingPlaylists = await db.Playlists
                .Where(p => p.UserId == userId)
                .ToListAsync();

            if ( existingPlaylists.Count >= PlaylistUserLimt )
            {
                var playlistLimitMsg = "You currently have **5 playlists**, which is the maximum allowed. Here are your existing playlists:";
                var embed = EmbedFactory.CreateResponseEmbed(playlistLimitMsg, ResponseType.Error)
                    .AddFields(existingPlaylists
                        .Select(p => new EmbedFieldProperties()
                        {
                            Name = p.Name,
                            Value = " ",
                            Inline = false
                        })
                        .ToArray());
                await responseService.SendEmbedResponse(Context, embed);
            }

            await db.Playlists.AddAsync(new Playlist
            {
                UserId = userId,
                Name = name
            });

            await db.SaveChangesAsync();

            await responseService.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed($"Playlist '{name}' created successfully!", 
                ResponseType.Success));
        }
        catch ( Exception ex )
        {
            logger.LogError(ex.Message);

            await responseService.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed("Sorry! There was an error adding you playlist.", 
                ResponseType.Error));
        }
    }


    [SubSlashCommand("remove", "delete a playlist")]
    public async Task RemovePlaylistAsync(
        [SlashCommandParameter(
            Name = "name",
            Description = "Name of the playlist to add. If none, open a form."
        )]
        string? name = null)
    {
        // Defer the response to give more time for processing
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        //await EnsureUserAsync(Context);

        var userId = (int)Context.Interaction.User.Id;

        // Get the user's playlists from the database
        var playlists = db.Playlists.Where(t => t.UserId == userId);


        // Check if the user actually has any playlists to choose from
        if (!playlists.Any())
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "You don't have any playlists to delete.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        // If the name specified then do a direct search and delete
        if (name != null)
        {
            Playlist? playlistToDelete = playlists
                .FirstOrDefault(p => p.Name == name && p.UserId == userId);

            if (playlistToDelete != null)
            {
                db.Playlists.Remove(playlistToDelete);
                await db.SaveChangesAsync();
                await Context.Interaction.SendFollowupMessageAsync(new()
                {
                    Content = "Deleted playlist.",
                    Flags = MessageFlags.Ephemeral
                });
                return;
            }

            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "Nothing to delete.",
                Flags = MessageFlags.Ephemeral
            });
            return;
        }

        // If unspecified name, then show user their playlists and let them choose which to delete.
        // Build an interactive menu for the user that will show in their chat window
        var rows = playlists
            .Select(a =>
                new StringMenuSelectOptionProperties(
                    label: "Playlist Name",
                    value: a.Name)
            ).ToArray();

        // Create a button component
        var button = new ButtonProperties(
            customId: "delete_playlist_btn:{playlistId}",
            label: "Click Me!",
            style: ButtonStyle.Primary
        );

        var actionRow = new ActionRowProperties
            {
                button
            };

        var menu = new StringMenuProperties("delete_playlist_menu")
        {
            Options = [
                new StringMenuSelectOptionProperties("Playlist 1", "1"),
                    new StringMenuSelectOptionProperties("Playlist 2", "2")
            ]
        };

        // Send the button back to the user
        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = "Click the button below:",
            Components = [actionRow, menu],
            Flags = MessageFlags.Ephemeral
        });
        return;
    }


    [SubSlashCommand("rename", "rename a playlist")]
    public async Task RenamePlaylistAsync(string newName, string? originalName = null)
    {
        // FEAT: This could be a form that lets the user change multiple meta aspects of the playlist.
        // For now, just change the name.
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await validationService.EnsureUserAsync(Context.User.Id, Context.User.Username);

        var userId = (int)Context.Interaction.User.Id;

        var playlist = db.Playlists.FirstOrDefault(
            p => p.UserId == userId && p.Name == originalName);

        if (playlist == null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Playlist '{originalName}' not found."
            });
            return;
        }

        playlist.Name = newName;
        await db.SaveChangesAsync();

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Renamed playlist '{originalName}' → '{newName}'."
        });
    }


    [SubSlashCommand("track", "track actions")]
    public class TrackActionMenu (
        AbarBotDbContext db): ApplicationCommandModule<ApplicationCommandContext>
    {
        [SubSlashCommand("add", "add a track")]
        public async Task AddTrackAsync(string playlistName, string trackQuery)
        {
            // FEAT: What if this had some kind of memory of the last playlist the user was working with,
            // and then it would just add to that playlist by default?
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
            //await EnsureUserAsync(Context);

            var userId = (int)Context.Interaction.User.Id;

            var playlist = db.Playlists.FirstOrDefault(
                p => p.UserId == userId && p.Name == playlistName);

            if (playlist == null)
            {
                await Context.Interaction.SendFollowupMessageAsync(new()
                {
                    Content = $"Playlist '{playlistName}' not found."
                });
                return;
            }

            await db.Sounds.AddAsync(new()
            {
                PlaylistId = playlist.Id,
                FileName = trackQuery
            });

            await db.SaveChangesAsync();

            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Added track to '{playlistName}': {trackQuery}"
            });
        }


        [SubSlashCommand("delete", "delete a track")]
        public async Task RemoveTrackAsync(string playlistName, string trackQuery)
        {
            await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
            //await EnsureUserAsync(Context);

            var userId = (int)Context.Interaction.User.Id;

            var playlist = db.Playlists.FirstOrDefault(
                p => p.UserId == userId && p.Name == playlistName);

            if (playlist == null)
            {
                await Context.Interaction.SendFollowupMessageAsync(new()
                {
                    Content = $"Playlist '{playlistName}' not found."
                });
                return;
            }

            var track = db.Sounds.FirstOrDefault(
                t => t.PlaylistId == playlist.Id && t.FileName == trackQuery);

            if (track == null)
            {
                await Context.Interaction.SendFollowupMessageAsync(new()
                {
                    Content = $"Track not found in '{playlistName}'."
                });
                return;
            }

            db.Sounds.Remove(track);
            await db.SaveChangesAsync();

            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Removed track from '{playlistName}'."
            });
        }
    }
}
