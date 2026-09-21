using Microsoft.EntityFrameworkCore;

using NetCord.Abar.Bot.Models;

using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;


namespace NetCord.Abar.Bot.SlashCommands;


public class PlaylistModalButtons: ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly AbarBotDbContext _db;
    public PlaylistModalButtons(AbarBotDbContext db)
    {
        _db = db;
    }

    [ComponentInteraction("button")]
    public string Button() => "I was clicked";


    [ComponentInteraction("delete_playlist_menu")]
    public async Task<string> HandleDeletePlaylistMenuAsync(string selectedValue)
    {
        int playlistId = int.Parse(selectedValue);
        var userId = (long)Context.User.Id;

        var playlist = await _db.Playlists
            .FirstOrDefaultAsync(p => p.Id == playlistId && p.UserId == userId);

        if (playlist == null)
            return "Playlist not found";

        _db.Playlists.Remove(playlist);
        await _db.SaveChangesAsync();

        return "Playlist deleted";
    }
}


public class PlaylistModule : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly AbarBotDbContext _db;

    public PlaylistModule(AbarBotDbContext db)
    {
        _db = db;
    }


    private async Task EnsureUserAsync(ulong discordUserId, string discordUserName)
    {
        // NOTE: Could use a middleware pattern instead,
        // but that would mean creating a custom module.
        // This is a low effort way to get the same effect.
        int userId = (int)discordUserId;

        Models.User? existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        // If user exists update them
        if (existingUser != null)
        {
            // Update user data
            existingUser.Name = discordUserName;
            if (existingUser.Name != discordUserName)
            {
                await _db.SaveChangesAsync();
                return;
            }
        }
        else
        {
            await _db.Users.AddAsync(new Models.User
            {
                Id = userId,
                Name = discordUserName,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }


    [SlashCommand("playlistadd", "create a new private playlist")]
    public async Task AddPlaylistAsync(string? name = null)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await EnsureUserAsync(Context.Interaction.User.Id, Context.Interaction.User.Username);

        // FEAT: Make default user playlist config driven
        if (string.IsNullOrWhiteSpace(name))
            name = "My Playlist";

        var userId = (int)Context.Interaction.User.Id;
        var existingPlaylists = _db.Playlists.Where(p => p.UserId == userId);

        // FEAT: Make config driven for the max number of playlists a user can have
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

        // Create a new playlist and save it to the database
        await _db.Playlists.AddAsync(new Playlist
        {
            UserId = userId,
            Name = name
        });
        await _db.SaveChangesAsync();

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
        await EnsureUserAsync(Context.Interaction.User.Id, Context.Interaction.User.Username);

        var userId = (int)Context.Interaction.User.Id;

        // Get the user's playlists from the database
        var playlists = _db.Playlists.Where(t => t.UserId == userId);


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
                _db.Playlists.Remove(playlistToDelete);
                await _db.SaveChangesAsync();
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
        await EnsureUserAsync(Context.Interaction.User.Id, Context.Interaction.User.Username);

        var userId = (int)Context.Interaction.User.Id;

        var playlist = _db.Playlists.FirstOrDefault(
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
        await _db.SaveChangesAsync();

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
        await EnsureUserAsync(Context.Interaction.User.Id, Context.Interaction.User.Username);

        var userId = (int)Context.Interaction.User.Id;

        var playlist = _db.Playlists.FirstOrDefault(
            p => p.UserId == userId && p.Name == playlistName);

        if (playlist == null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Playlist '{playlistName}' not found."
            });
            return;
        }

        await _db.Sounds.AddAsync(new()
        {
            PlaylistId = playlist.Id,
            FileName = trackQuery
        });

        await _db.SaveChangesAsync();

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
        await EnsureUserAsync(Context.Interaction.User.Id, Context.Interaction.User.Username);

        var userId = (int)Context.Interaction.User.Id;

        var playlist = _db.Playlists.FirstOrDefault(
            p => p.UserId == userId && p.Name == playlistName);

        if (playlist == null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Playlist '{playlistName}' not found."
            });
            return;
        }

        var track = _db.Sounds.FirstOrDefault(
            t => t.PlaylistId == playlist.Id && t.FileName == trackQuery);

        if (track == null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Track not found in '{playlistName}'."
            });
            return;
        }

        _db.Sounds.Remove(track);
        await _db.SaveChangesAsync();

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Removed track from '{playlistName}'."
        });
    }
}
