using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

using NetCord.Abar.Bot.Definitions.Models;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Tools.Factories;


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
        [SlashCommandParameter(
            Name = "name", 
            Description = "Name of the playlist to add. If none, open a form."
        )]
        string? name = null)
    {
        try
        {
            await validationService.EnsureUserAsync(Context);

            if ( string.IsNullOrWhiteSpace(name) )
            {
                // This works if I dont defer the response before calling respond
                await RespondAsync(InteractionCallback.Modal(ModalFactory.CreateActionSelectModal()));

                return;
            }

            await responseService.DeferAsync(Context, true);

            int userId = ( int )Context.Interaction.User.Id;

            List<Playlist> existingPlaylists = await db.Playlists
                .Where(p => p.UserId == userId)
                .ToListAsync();

            if ( existingPlaylists.Count >= PlaylistUserLimt )
            {
                string playlistLimitMsg = "You currently have **5 playlists**, " +
                    "which is the maximum allowed. Here are your existing playlists:";

                EmbedProperties embed = EmbedFactory.CreateResponseEmbed(playlistLimitMsg, ResponseType.Error)
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
        try
        {
            await validationService.EnsureUserAsync(Context);

            if ( string.IsNullOrWhiteSpace(name) )
            {
                var modal = await ModalFactory.CreatePlaylistRemoveModal(Context.User.Id, db);
                await RespondAsync(InteractionCallback.Modal(modal));
                return;
            }

            int userId = (int)Context.Interaction.User.Id;

            await responseService.DeferAsync(Context, true);

            // Get the user's playlists from the database
            List<Playlist> playlists = await db.Playlists
                .Where(t => t.UserId == userId)
                .ToListAsync();

            // Check if the user actually has any playlists to choose from
            if ( playlists.Count == 0 )
            {
                await responseService.SendEmbedResponse(
                    Context,
                    EmbedFactory.CreateResponseEmbed($"No playlists to delete!",
                    ResponseType.Warning));

                return;
            }

            Playlist? playlistToDelete = playlists
                .FirstOrDefault(p => p.Name == name && p.UserId == userId);

            if ( playlistToDelete == null )
            {
            await responseService.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    $"Couldn't find the playlist you specified: {name}", 
                    ResponseType.Warning));
            }

            db.Playlists.Remove(playlistToDelete!);

            await db.SaveChangesAsync();

            await responseService.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    "Playlist deleted.",
                    ResponseType.Success));
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);

            await responseService.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed("Sorry! There was an error adding you playlist.",
                ResponseType.Error));
        }
    }


    [SubSlashCommand("rename", "rename a playlist")]
    public async Task RenamePlaylistAsync(
        [SlashCommandParameter(
            Name = "playlist",
            Description = "Name of the existing playlist."
        )]
        string? playlist = null,

        [SlashCommandParameter(
            Name = "newName",
            Description = "New playlist name."
        )]
        string? newName = null)
    {
        await validationService.EnsureUserAsync(Context);

        // If any args are null, show a modal
        if ( playlist == null || newName == null )
        {
            var modal = ModalFactory.CreatePlaylistRenameModal();
            
            await RespondAsync(InteractionCallback.Modal(modal));
            
            return;
        }

        var userId = (int)Context.Interaction.User.Id;

        var userPlaylist = await db.Playlists
            .Where(p => p.UserId == userId && p.Name == playlist)
            .FirstAsync();

        if ( playlist == null )
        {
            await responseService.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    "Couldn't find that playlist to rename",
                    ResponseType.Warning));
            
            return;
        }

        userPlaylist.Name = newName;

        await db.SaveChangesAsync();

        await responseService.SendEmbedResponse(
            Context,
            EmbedFactory.CreateResponseEmbed(
                $"Renamed playlist '{playlist}' → '{newName}'.",
                ResponseType.Success));
    }


    [SubSlashCommand("track", "track actions")]
    public class TrackActionMenu(
            AbarBotDbContext db,
            ResponseService responses,
            ValidationService validationService
        ) : ApplicationCommandModule<ApplicationCommandContext>
    {
        [SubSlashCommand("add", "add a track to a playlist")]
        public async Task AddTrackAsync(
            [SlashCommandParameter(
                Name = "playlistName",
                Description = "Name of the target playlist"
            )]
            string playlistName,

            [SlashCommandParameter(
                Name = "trackQuery",
                Description = "Id of the track being added"
            )]
            string trackQuery)
        {
            await validationService.EnsureUserAsync(Context);

            if ( playlistName == null || trackQuery == null )
            {
                await RespondAsync(InteractionCallback.Modal(
                    ModalFactory.CreateTrackAddModal()));

                return;
            }

            var userId = (int)Context.Interaction.User.Id;

            var playlist = await db.Playlists
                .Where(p => p.UserId == userId && p.Name == playlistName)
                .FirstOrDefaultAsync();

            if (playlist == null)
            {
                await responses.SendEmbedResponse(
                    Context,
                    EmbedFactory.CreateResponseEmbed(
                        $"Playlist '{playlistName}' not found.",
                        ResponseType.Warning));

                return;
            }

            await db.Sounds.AddAsync(new()
            {
                PlaylistId = playlist.Id,
                FileName = trackQuery
            });

            await db.SaveChangesAsync();

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    "Added track to playlist!",
                    ResponseType.Success));
        }


        [SubSlashCommand("delete", "delete a track")]
        public async Task RemoveTrackAsync(
            [SlashCommandParameter(
                Name = "playlistName",
                Description = "Name of the target playlist"
            )]
            string playlistName,

            [SlashCommandParameter(
                Name = "trackQuery",
                Description = "Id of the track being removed"
            )]
            string trackQuery)
        {
            await validationService.EnsureUserAsync(Context);

            // If any args are null, show a modal
            if ( playlistName == null || trackQuery == null )
            {
                await RespondAsync(InteractionCallback.Modal(
                    ModalFactory.CreateTrackDeleteModal()));
            }

            var userId = ( int )Context.Interaction.User.Id;

            var playlist = db.Playlists
                .Where(p => p.UserId == userId && p.Name == playlistName)
                .FirstOrDefaultAsync();

            if ( playlist == null )
            {
                await responses.SendEmbedResponse(
                    Context,
                    EmbedFactory.CreateResponseEmbed(
                        $"Playlist '{playlistName}' not found.",
                        ResponseType.Warning));

                return;
            }

            var track = await db.Sounds
                .Where(t => t.PlaylistId == playlist.Id && t.FileName == trackQuery)
                .FirstOrDefaultAsync();

            if ( track == null )
            {
                await responses.SendEmbedResponse(
                    Context,
                    EmbedFactory.CreateResponseEmbed(
                        $"Track '{trackQuery}' not found in playlist '{playlistName}'.",
                        ResponseType.Warning));

                return;
            }

            db.Sounds.Remove(track);

            await db.SaveChangesAsync();

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    "Removed track!",
                    ResponseType.Success));
        }
    }
}
