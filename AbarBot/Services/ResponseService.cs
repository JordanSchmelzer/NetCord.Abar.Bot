using NetCord.Abar.Bot.Tools.Factories;
using NetCord.Rest;
using NetCord.Services;


namespace NetCord.Abar.Bot.Services;


public sealed class ResponseService
{
    public async Task DeferAsync(IInteractionContext ctx, bool ephemeral = false)
    {
        await ctx.Interaction.SendResponseAsync(
            InteractionCallback.DeferredMessage(ephemeral ? MessageFlags.Ephemeral : null));
    }


    public async Task ModifyStatusResponse(IInteractionContext ctx, string message)
    {
        var embed = EmbedFactory.CreatePlainEmbed(message);

        await ctx.Interaction.ModifyResponseAsync(options =>
        {
            options.Embeds = [embed];
        });
    }


    public Task ModifyEmbedResponse(IInteractionContext ctx, EmbedProperties embed)
    {
        return ctx.Interaction.ModifyResponseAsync(options =>
        {
            options.Embeds = [embed];
        });
    }


    public async Task SendPlainResponse(IInteractionContext ctx, string message)
    {
        var embed = EmbedFactory.CreatePlainEmbed(message);
        var properties = new InteractionMessageProperties().AddEmbeds(embed);

        await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(properties));
    }


    public async Task SendEmbedResponse(IInteractionContext ctx, EmbedProperties embed)
    {
        var properties = new InteractionMessageProperties().AddEmbeds(embed);
        await ctx.Interaction.SendResponseAsync(InteractionCallback.Message(properties));
    }



}
