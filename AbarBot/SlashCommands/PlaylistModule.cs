using Microsoft.EntityFrameworkCore;

using NetCord.Abar.Bot.Models;

using NetCord.Rest;
using NetCord.Services.ApplicationCommands;


namespace NetCord.Abar.Bot.SlashCommands;


public class PlaylistModule : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly AbarBotDbContext _db;

    public PlaylistModule(AbarBotDbContext db)
    {
        _db = db;
    }


    // TODO: these are just placeholders for now
    private async Task<bool> PlaylistExistsAsync(int userId, string name)
    {
        return await _db.Playlists.AnyAsync(p => p.UserId == userId && p.Name == name);
    }

    private async Task<bool> TrackExistsAsync(int userId, string playlistName, string trackName)
    {
        var playlist = await _db.Playlists.FirstOrDefaultAsync(p => p.UserId == userId && p.Name == playlistName);
        if (playlist == null)
        {
            return false;
        }
        return await _db.Sounds.AnyAsync(t => t.PlaylistId == playlist.Id && t.FileName == trackName);
    }

    // NOTE: Could use a middleware pattern instead, but that would mean creating a custom module.
    // This is a low effort way to get the same effect. Pros and cons to both strategy.
    private async Task EnsureUserAsync(ulong discordUserId, string discordUserName)
    {
        int userId = (int)discordUserId;

        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (existingUser != null)
        {
            // Update the user's name if it has changed
            existingUser.Name = discordUserName;

            await _db.SaveChangesAsync();
            return;
        }

        await _db.Users.AddAsync(new Models.User
        {
            Id = userId,
            Name = discordUserName,
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }


    [SlashCommand("playlistadd", "create a new private playlist")]
    public async Task AddPlaylistAsync(string? name = null)
    {
        // Defer the response to give more time for processing
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        await EnsureUserAsync(Context.Interaction.User.Id, Context.Interaction.User.Username);

        // Treat null as using the default name "My Playlist"
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "My Playlist";
        }

        // Get the user ID of the user who invoked the command
        var userId = (int)Context.Interaction.User.Id;

        // Check if the user already has X or more playlists
        var existingPlaylists = _db.Playlists.Where(d => d.UserId == userId);

        // If the user has X or more playlists, send a follow-up message and return
        // TODO: Make config driven for the max number of playlists a user can have
        if (existingPlaylists.Count() >= 5) 
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = "Sorry! You can't have more than 5 playlists. Please delete one."
            });
            // FEAT: Would be rad if an interactive embed could be used to select and delete a playlist here,
            // but that is a future feature.
            return;
        }

        // Create a new playlist and save it to the database
        await _db.Playlists.AddAsync(new Playlist
        {
            UserId = userId,
            Name = name
        });
        await _db.SaveChangesAsync();

        // Let the user know that the playlist was created successfully
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

        // Use the default name "My Playlist" if no name is provided
        // TODO: Let the app ower set this in the config file. For now, hardcode it.
        if (name == null)
        {
            name = "My Playlist";
        }

        // If the playlist doesnt exist, delete it and send a follow-up message
        Playlist? playlistToDelete = playlists.FirstOrDefault(p => p.Name == name);
        if (playlistToDelete == null)
        {
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = $"Playlist '{name}' not found"
            });
            return;
        }

        // If the playlist exists, send a follow-up message indicating that
        _db.Playlists.Remove(playlistToDelete);
        await _db.SaveChangesAsync(); // TODO: Err handling and also unsure if async works with sqllite

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Playlist '{name}' deleted..."
        });
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
