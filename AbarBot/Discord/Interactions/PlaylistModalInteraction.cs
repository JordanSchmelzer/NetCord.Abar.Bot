using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using NetCord.Rest;
using NetCord.Services.ComponentInteractions;

using NetCord.Abar.Bot.Definitions.Models;
using NetCord.Abar.Bot.Models.Exceptions;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Tools.Factories;


namespace NetCord.Abar.Bot.Discord.Interactions;


public class PlaylistStringMenuInteractionProperties(
    AbarBotDbContext db,
    ResponseService responses,
    ILogger<PlaylistModalInteraction> logger)
    : ComponentInteractionModule<StringMenuInteractionContext>
{
    [ComponentInteraction("manage_playlists")]
    public async Task HandlePlaylistManageActionSelect()
    {
        try
        {
            ulong userId = Context.Interaction.User.Id;

            string action = Context.SelectedValues.First();

            ModalProperties modal = action switch
            {
                "Add" => ModalFactory.CreatePlaylistAddModal(),
                "Remove" => await ModalFactory.CreatePlaylistRemoveModal(userId, db),
                "Rename" => ModalFactory.CreatePlaylistRenameModal(),
                _ => throw new InvalidRequestException($"Unknown playlist manage action: {action}")
            };

            await RespondAsync(InteractionCallback.Modal(modal));
        }
        catch (InvalidRequestException e)
        {
            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    "You somehow selected an unsupported action! How'd you manage to do that?",
                    ResponseType.Warning));
        }
        catch (Exception ex)
        {
            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed("Sorry! An error occured.", ResponseType.Error));
        }
    }
}


public class PlaylistModalInteraction(
    AbarBotDbContext db,
    ResponseService responses,
    ILogger<PlaylistModalInteraction> logger)
    :ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction("manage_playlists")]
    public async Task HandlePlaylistManageActionSelect()
    {
        try
        {
            ulong userId = Context.Interaction.User.Id;
            var components = Context.Components
                .OfType<Label>()
                .Select(label => label.Component)
                .ToArray();

            var actionChoice =
                GetComponent<StringMenu>(components, "playlist-action");

            string action = actionChoice.SelectedValues!.First();

            ModalProperties modal = action switch
            {
                "Add" => ModalFactory.CreatePlaylistAddModal(),
                "Remove" => await ModalFactory.CreatePlaylistRemoveModal(userId, db),
                "Rename" => ModalFactory.CreatePlaylistRenameModal(),
                _ => throw new InvalidRequestException($"Unknown playlist manage action: {action}")
            };

            await RespondAsync(InteractionCallback.Modal(modal));
            return;
        }
        catch (InvalidRequestException e)
        {
            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed(
                    "You somehow selected an unsupported action! How'd you manage to do that?", 
                    ResponseType.Warning));
        }
        catch (Exception ex)
        {
            await responses.SendEmbedResponse(
                Context, 
                EmbedFactory.CreateResponseEmbed("Sorry! An error occured.", ResponseType.Error));
        }
    }


    [ComponentInteraction("playlist-add-one")]
    public async Task DoThing()
    {
        var deferred = false;

        await RespondAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
        deferred = true;

        var components = Context.Components
            .OfType<Label>()
            .Select(label => label.Component)
            .ToArray();

        var playlistName =
            GetComponent<TextInput>(components, "playlist-name");

        await Context.Interaction.SendFollowupMessageAsync(new()
        {
            Content = $"Added playlist: {playlistName.Value}"
        });           
    }


    [ComponentInteraction("playlist-rename")]
    public async Task HandleRenamePlaylist(string playlistId, string newName)
    {
        if (!int.TryParse(playlistId, out int id))
        {
            await responses.SendEmbedResponse(
                Context,
                EmbedFactory.CreateResponseEmbed($"Invalid playlist Id: {playlistId}",ResponseType.Error)
            );
            return;
        }

        try
        {
            Playlist playlist = await db.Playlists.Where(p => p.Id == id).FirstAsync();
            string originalName = playlist.Name;

            playlist.Name = newName;
            await db.SaveChangesAsync();
       
            await responses.SendEmbedResponse(
                Context, 
                EmbedFactory.CreateResponseEmbed(
                    $"Renamed {originalName} to {newName}", 
                    ResponseType.Success)
            );
            return;
        }
        catch (Exception ex)
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
