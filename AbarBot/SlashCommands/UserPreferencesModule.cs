using NetCord.Services.ApplicationCommands;
using System;
namespace NetCord.Abar.Bot.SlashCommands;


public class UserPreferencesModule : ApplicationCommandModule<ApplicationCommandContext>
{
    private readonly AbarBotDbContext _db;

    public UserPreferencesModule(AbarBotDbContext db)
    {
        _db = db;
    }


    [SlashCommand("playlistnew", "create a new private playlist")]
    public async Task CreateNewPlaylistAsync()
    {

    }


    [SlashCommand("playlistdelete", "delete playlist")]
    public async Task DeletePlaylistAsync()
    {

    }


    [SlashCommand("playlistadd", "delete my personal playlist")]
    public async Task DeleteMyPlaylistAsync()
    {

    }


    [SlashCommand("delete", "i")]
    public async Task AddTrackAsync()
    {

    }


    [SlashCommand("i", "j")]
    public async Task RemoveTrackAsync()
    {

    }


}
