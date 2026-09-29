using Microsoft.EntityFrameworkCore;

using NetCord.Rest;

using NetCord.Abar.Bot.Definitions.Models;


namespace NetCord.Abar.Bot.Tools.Factories;


public static class ModalFactory
{
    public static ModalProperties CreatePlaylistAddModal()
    {
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
        ulong userId,
        AbarBotDbContext db)
    {
        var playlists = await db.Playlists
            .Where(p => p.UserId == (int)userId)
            .ToListAsync();

        IEnumerable<StringMenuSelectOptionProperties> options = playlists
            .Select(p => new StringMenuSelectOptionProperties(
                label: "p.Name",
                value: p.Id.ToString()))
            .ToArray();

        return new ModalProperties("playlist-remove-one", "Remove Playlist")
            .AddComponents(
                new LabelProperties("Select Playlist",
                    new StringMenuProperties("playlist-select", options)
                        .WithRequired())
                .WithDescription("Choose which playlist to remove.")
            );
    }


    public static ModalProperties CreatePlaylistRenameModal()
    {
        return new ModalProperties("playlist-rename", "Rename Playlist")
            .AddComponents(
                // Which playlist to rename
                new LabelProperties("Playlist ID:",
                    new StringMenuProperties("playlist-id", EnumTools.CreateSelectOptions<PlaylistActions>())
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


    public static ModalProperties CreateActionSelectModal()
    {
        return new ModalProperties("playlist-actions", "Choose an Action:")
            .AddComponents(
                new LabelProperties("Playlist Action:",
                    new StringMenuProperties("playlist-action", EnumTools.CreateSelectOptions<PlaylistActions>())
                )
            );
    }


    public static ModalProperties CreateModal()
    {
        return new ModalProperties("example-id", "example");
    }
}
