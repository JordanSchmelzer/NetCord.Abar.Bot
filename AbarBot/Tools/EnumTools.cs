using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System.Reflection;
using NetCord.Abar.Bot.Models.Exceptions;


namespace NetCord.Abar.Bot.Tools
{
    public static class EnumTools
    {
        public static string GetDisplayName<TEnum>(TEnum value)
            where TEnum : struct, Enum
        {
            var name = value.ToString();
            return typeof(TEnum).GetField(name)?.GetCustomAttribute<SlashCommandChoiceAttribute>()?.Name ?? name;
        }

        public static IEnumerable<StringMenuSelectOptionProperties> CreateSelectOptions<TEnum>()
            where TEnum : struct, Enum
        {
            return Enum.GetValues<TEnum>().Select(value =>
                new StringMenuSelectOptionProperties(GetDisplayName(value), value.ToString()));
        }

        public static TEnum Parse<TEnum>(string value)
            where TEnum : struct, Enum
        {
            if (!Enum.TryParse<TEnum>(value, out var result) || !Enum.IsDefined(result))
                throw new InvalidInputException(value, $"Invalid {typeof(TEnum).Name} value.");

            return result;
        }
    }
}