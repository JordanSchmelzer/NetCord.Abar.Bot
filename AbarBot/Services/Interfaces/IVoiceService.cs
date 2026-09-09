using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace NetCord.Abar.Bot.Services.Interfaces;


public interface IVoiceService
{
    public Task<InteractionMessageProperties> JoinAsync(
        ApplicationCommandContext ctx, 
        IVoiceGuildChannel? channel = null);
    public Task<InteractionMessageProperties> LeaveAsync(
        ApplicationCommandContext ctx);
}
