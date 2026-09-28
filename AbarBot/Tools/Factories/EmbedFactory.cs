using NetCord.Rest;
using NetCord.Abar.Bot.Definitions.Models;


namespace NetCord.Abar.Bot.Tools.Factories;


public static class EmbedFactory
{
    public static readonly Color AbarColor = new(0xFA64D0);

    private const string AuthorName = "Abar.Bot";


    public static EmbedProperties CreatePlainEmbed(string message)
    {
        return CreateDefaultEmbed().WithDescription(message);
    }


    public static EmbedProperties CreateResponseEmbed(string message, ResponseType type)
    {
        var (color, status) = type switch
        {
            ResponseType.Success => (new Color(0x48B02C), "Success"),
            ResponseType.Warning => (new Color(0xFFBB33), "Warning"),
            ResponseType.Error => (new Color(0xFF4141), "Error"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        return new EmbedProperties()
            .WithColor(color)
            .WithAuthor(new EmbedAuthorProperties()
                .WithName($"{AuthorName} ・ {status}")
            )
            .WithDescription(message)
            .WithFooter(new EmbedFooterProperties()
                .WithText($"{AuthorName}- Automated response")
            );
    }


    private static EmbedProperties CreateDefaultEmbed()
    {
        return new EmbedProperties()
            .WithTimestamp(DateTime.UtcNow);
    }
}
