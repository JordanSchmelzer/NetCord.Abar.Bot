using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using System;
using System.Collections.Generic;
using System.Text;

namespace NetCord.Abar.Bot.Commands
{
    public class HelpController: ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly ApplicationCommandService<ApplicationCommandContext> _commandService;


        public HelpController(ApplicationCommandService<ApplicationCommandContext> commandService)
        {
            _commandService = commandService;
        }


        [SlashCommand("help", "Shows all available commands")]
        public async Task HelpAsync()
        {
            // Acknowledge the interaction as right away
            await Context.Interaction.SendResponseAsync(
                InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

            // Read the commands directly
            var lines = _commandService.GetCommands()
                .OfType<SlashCommandInfo<ApplicationCommandContext>>() // Filter and safely cast to expose Description
                .Select(c => $"• **/{c.Name}** — {c.Description}")
                .ToList();

            string helpText = lines.Count > 0
                ? string.Join("\n", lines)
                : "No slash commands found.";

            // The followup will automatically inherit the ephemeral status from the deferral
            await Context.Interaction.SendFollowupMessageAsync(new()
            {
                Content = helpText
            });
        }
    }
}
