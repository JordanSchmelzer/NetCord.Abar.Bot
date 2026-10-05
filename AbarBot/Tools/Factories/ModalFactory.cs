using Microsoft.EntityFrameworkCore;

using NetCord.Rest;

using NetCord.Abar.Bot.Definitions.Models;
using NetCord.Services.ApplicationCommands;


namespace NetCord.Abar.Bot.Tools.Factories;


public static class ModalFactory
{
    public static ModalProperties CreatePlaylistAddModal(
        ApplicationCommandContext ctx, 
        AbarBotDbContext db)
    {
        // Interaction expects "playlist-name" as text input field name.
        return new ModalProperties("playlist-add-one", "Add New Playlist")
            .AddComponents(
                new LabelProperties("Playlist Name",
                    new TextInputProperties("playlist-name", TextInputStyle.Short)
                        .WithRequired()
                        .WithMinLength(1)
                        .WithMaxLength(30)
                        .WithPlaceholder("e.g. Rock")
                )
            );
    }


    public static async Task<ModalProperties> CreatePlaylistRemoveModal(
        ApplicationCommandContext ctx,
        AbarBotDbContext db)
    {
        var playlists = await db.Playlists
            .Where(p => p.UserId == ( int )ctx.User.Id)
            .ToListAsync();

        IEnumerable<StringMenuSelectOptionProperties> options = playlists
            .Select(p => new StringMenuSelectOptionProperties(
                label: "Playlist Name:",
                value: p.Name))
            .ToArray();

        return new ModalProperties("playlist-remove-one", "Remove Playlist")
            .AddComponents(
                new LabelProperties("Select Playlist",
                    new StringMenuProperties("playlist-name", options)
                        .WithRequired())
                .WithDescription("Choose which playlist to remove.")
            );
    }


    public async static Task<ModalProperties> CreatePlaylistRenameModal(
        ApplicationCommandContext ctx,
        AbarBotDbContext db)
    {
        var playlists = await db.Playlists
            .Where(p => p.UserId == (int)ctx.User.Id)
            .ToListAsync();

        IEnumerable<StringMenuSelectOptionProperties> options = playlists
            .Select(p => new StringMenuSelectOptionProperties(
                label: "Playlist Name:",
                value: p.Name))
            .ToArray();

        return new ModalProperties("playlist-rename", "Rename Playlist")
            .AddComponents(
                // Which playlist to rename
                new LabelProperties("Playlist ID:",
                    new StringMenuProperties("playlist-id", options)
                        .WithRequired()
                ),

                // New name for the playlist
                new LabelProperties("New Name:",
                    new TextInputProperties("playlist-new-name", TextInputStyle.Short)
                        .WithRequired()
                        .WithMinLength(1)
                        .WithMaxLength(30)
                        .WithPlaceholder("e.g. Rock")
                )
            );
    }

    /*
    public async static ModalProperties CreateTrackDeleteModal(
        ApplicationCommandContext ctx,
        AbarBotDbContext db)
    {
        var playlists = await db.Playlists
            .Where(p => p.UserId == (int)ctx.User.Id)
            .ToListAsync();

        var tracks = await db.Tracks
            .Where(t => playlists.Select(p => p.Id).Contains(t.PlaylistId))
            .ToListAsync();

        return new ModalProperties("track-delete-one", "Delete Track")
            .AddComponents(
                new LabelProperties("Select Track",
                    new StringMenuProperties("track-select")
                    {
                        Options = options
                    }
                    .WithRequired()
                    .WithMaxValues(1)
                )
                .WithDescription("Choose which track to delete.")
            );
    }


    public async static ModalProperties CreateTrackAddModal(
        ApplicationCommandContext ctx,
        AbarBotDbContext db)
    {
        var playlists = await db.Playlists
            .Where(p => p.UserId == (int)ctx.User.Id)
            .ToListAsync();

        var playlistOptions = playlists
            .Select(p => new StringMenuSelectOptionProperties(
                label: p.Name,
                value: p.Id.ToString()))
            .ToArray();

        return new ModalProperties("track-add-one", "Add Track")
            .AddComponents(
                // Playlist selection
                new LabelProperties("Playlist",
                    new StringMenuProperties("track-add-playlist")
                    {
                        Options = playlistOptions
                    }
                    .WithRequired()
                    .WithMaxValues(1)
                ),

                // Track name
                new LabelProperties("Track Name",
                    new TextInputProperties("track-add-name", TextInputStyle.Short)
                        .WithRequired()
                        .WithMinLength(1)
                        .WithMaxLength(100)
                        .WithPlaceholder("e.g. Through the Fire and Flames")
                ),

                // Track URL
                new LabelProperties("Track URL",
                    new TextInputProperties("track-add-url", TextInputStyle.Short)
                        .WithRequired()
                        .WithMinLength(5)
                        .WithMaxLength(200)
                        .WithPlaceholder("https://example.com/track")
                )
            );
    }
    */
}
