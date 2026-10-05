using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

using NetCord.Abar.Bot.Definitions.Models;
using NetCord.Abar.Bot.Models.Exceptions;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Tools.Factories;


namespace NetCord.Abar.Bot.Discord.Interactions;

public class PlaylistModalInteraction(
    AbarBotDbContext db,
    ResponseService responses,
    ILogger<PlaylistModalInteraction> logger)
    :ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction("playlist-add-one")]
    public async Task AddOnePlaylstAsync()
    {
        try
        {
            string playlistNameCustomId = "playlist-name";

            var components = Context.Components
                .OfType<Label>()
                .Select(label => label.Component)
                .ToArray();

            var playlist = 
                GetComponent<TextInput>(components, playlistNameCustomId);

            await db.Playlists.AddAsync(new Playlist
            {
                Name = playlist.Value,
                UserId = (int)Context.User.Id
            });

            await db.SaveChangesAsync();

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    $"Added playlist: {playlist.Value}", 
                    ResponseType.Success));     
        }
        catch ( Exception ex )
        {
            logger.LogError(ex.Message);

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    $"Sorry! An error happened while processing your request.",
                    ResponseType.Error)
);
        }
    }


    [ComponentInteraction("playlist-remove-one")]
    public async Task RemoveOnePlaylistAsync()
    {
        try
        {
            // Find this value in the modal's components
            // It is the playlist name to remove
            string dataProperty = "playlist-name";

            var components = Context.Components
                .OfType<StringMenu>()
                .Select(label => label.CustomId)
                .ToArray();



            var playlist = await db.Playlists
                .Where(p => p.UserId == (int)Context.User.Id && p.Name == components.First())
                .FirstOrDefaultAsync();

            db.Playlists.Remove(playlist);

            await db.SaveChangesAsync();

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    $"Removed playlist: {components.First()}",
                    ResponseType.Success));
        }
        catch ( Exception ex )
        {
            logger.LogError(ex.Message);

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    $"Sorry! An error happened while processing your request.",
                    ResponseType.Error)
            );
        }
    }


    [ComponentInteraction("playlist-rename")]
    public async Task HandleRenamePlaylist(string playlistId, string newName)
    {
        try
        {
            // Find this value in the modals components
            // IT is the playlist name to rename
            string dataProperty1 = "playlist-id";
            string dataProperty2 = "playlist-new-name";

            Playlist playlist = await db.Playlists
                .Where(p => p.Id == 1)
                .FirstAsync();
            
            string originalName = playlist.Name;

            playlist.Name = newName;
            
            await db.SaveChangesAsync();
       
            await responses.SendEmbedResponse(
                Context, 
                EmbedFactory.CreateResponseEmbed(
                    $"Renamed {originalName} to {newName}", 
                    ResponseType.Success)
            );
        }
        catch ( Exception ex )
        {
            logger.LogError(ex.Message);

            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    $"Sorry! An error happened while processing your request.",
                    ResponseType.Error)
            );
        }
    }


    private static T GetComponent<T>(IEnumerable<ILabelComponent> components, string customId)
    where T : class, ILabelComponent
    {
        return components.OfType<T>().FirstOrDefault(component =>
                   component switch
                   {
                       ChannelMenu menu => menu.CustomId == customId,
                       StringMenu menu => menu.CustomId == customId,
                       CheckboxGroup group => group.CustomId == customId,
                       TextInput input => input.CustomId == customId,
                       _ => false
                   })
               ?? throw new InvalidRequestException($"Missing modal field: {customId}.");
    }
}
