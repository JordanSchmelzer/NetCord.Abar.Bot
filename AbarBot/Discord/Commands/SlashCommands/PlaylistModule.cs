using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetCord.Abar.Bot.Definitions.Models;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Tools;
using NetCord.Abar.Bot.Tools.Factories;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;


namespace NetCord.Abar.Bot.Discord.Commands.SlashCommands;


public class PlaylistModule(
    AbarBotDbContext db,
    ResponseService responses,
    ILogger<PlaylistModule> logger
    ) : ApplicationCommandModule<ApplicationCommandContext>
{
    private async Task EnsureUserAsync(ApplicationCommandContext ctx)
    {
        // Maybe put this in a validation service

        int userId = (int)Context.Interaction.User.Id;
        string discordUserName = Context.Interaction.User.Username;

        Definitions.Models.User? existingUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        // If user exists update them
        if (existingUser != null)
        {
            // Update user data
            existingUser.Name = discordUserName;
            if (existingUser.Name != discordUserName)
            {
                await db.SaveChangesAsync();
                return;
            }
        }
        else
        {
            await db.Users.AddAsync(new Definitions.Models.User
            {
                Id = userId,
                Name = discordUserName,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }


    [SlashCommand("playlist", "Manage your playlists")]
    public async Task PlaylistRoot()
    {
        var opts = EnumTools.CreateSelectOptions<PlaylistActions>();

        var menu = new StringMenuProperties(
                customId: "manage_playlists",
                options: opts);

        await RespondAsync(InteractionCallback.Message(new()
        {
            Components = [menu],
            Flags = MessageFlags.Ephemeral
        }));
    }


    [SlashCommand("playlistadd", "create a new private playlist")]
    public async Task AddPlaylistAsync(
        [SlashCommandParameter(
            Name = "name",
            Description = "Name of the playlist to add. If none, open a form."
        )]
        string? name = null)
    {
        //await responses.DeferAsync(Context, true);
        await EnsureUserAsync(Context);

        if (string.IsNullOrWhiteSpace(name))
        {
            // This works if I dont defer the response
            await RespondAsync(InteractionCallback.Modal(ModalFactory.CreateActionSelectModal()));
            return;
        }

        // If the user specified a playlist name then try to add it
        var userId = (int)Context.Interaction.User.Id;
        var existingPlaylists = db.Playlists.Where(p => p.UserId == userId);

        if (existingPlaylists.Count() >= 5) 
        {
            var embed = new EmbedProperties
            {
                Title = "Playlist Limit Reached",
                Description = "You currently have **5 playlists**, " +
                              "which is the maximum allowed.\n\n" +
                              "Here are your existing playlists:",
                Color = new Color(255, 80, 80),
                Fields = existingPlaylists
                    .Select(p => new EmbedFieldProperties()
                    {
                        Name = p.Name,
                        Value = " ", 
                        Inline = false
                    })
                    .ToArray()
            };

            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Embeds = [embed],
                Flags = MessageFlags.Ephemeral
            });
            return;
        }
        
        await db.Playlists.AddAsync(new Playlist
        {
            UserId = userId,
            Name = name
        });
        await db.SaveChangesAsync();

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Playlist '{name}' created successfully!"
        });
    }


    [SlashCommand("playlistdelete", "delete a playlist")]
    public async Task DeletePlaylistAsync(string? name = null)
    {
        // Defer the response to give more time for processing
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await EnsureUserAsync(Context);

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


    [SlashCommand("playlistrename", "change the name of a playlist")]
    public async Task ModifyPlaylistAsync(string newName, string? originalName = null)
    {
        // FEAT: This could be a form that lets the user change multiple meta aspects of the playlist.
        // For now, just change the name.
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await EnsureUserAsync(Context);

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


    // TODO: Actually implement sounds
    [SlashCommand("trackadd", "add a track to a playlist")]
    public async Task AddTrackAsync(string playlistName, string trackQuery)
    {
        // FEAT: What if this had some kind of memory of the last playlist the user was working with,
        // and then it would just add to that playlist by default?
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await EnsureUserAsync(Context);

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


    // TODO: Actually implement sounds
    [SlashCommand("trackremove", "remove a track from a playlist")]
    public async Task RemoveTrackAsync(string playlistName, string trackQuery)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await EnsureUserAsync(Context);

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
