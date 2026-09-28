using NetCord.Services.ApplicationCommands;


namespace NetCord.Abar.Bot.Definitions.Models;

public enum EnumExample
{
    None = 0,
    [SlashCommandChoice(Name = "command choice 1")]
    Thing1,
    Thing2,
    BothThings
}
