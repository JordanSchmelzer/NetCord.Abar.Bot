using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord.Abar.Bot.Commands;
using NetCord.Abar.Bot.Database;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using Microsoft.EntityFrameworkCore;


HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);


// Ensure user secrets are available regardless of environment
builder.Configuration.AddUserSecrets<Program>(optional: true);


// reads appsettings, env vars, user-secrets, CLI
string? token = builder.Configuration["Discord:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    string noDiscordTokenMessage = "Discord token not found in configuration. " +
        "Set it in user secrets, environment variables, or command-line args.";
    throw new InvalidOperationException(noDiscordTokenMessage);
}

// Database connection string goes here.
string dbPath = Path.Combine("Database", "abar_bot.db");
string connStr = $"Data Source={dbPath}";

builder.Services.AddDiscordGateway(options =>
{
    options.Token = token; // Sets the token from config
    options.Intents = GatewayIntents.GuildMessages
                      | GatewayIntents.DirectMessages
                      //| GatewayIntents.MessageContent
                      | GatewayIntents.DirectMessageReactions
                      | GatewayIntents.GuildMessageReactions
                      | GatewayIntents.GuildVoiceStates
                      | GatewayIntents.Guilds;
})
.AddGatewayHandlers(typeof(Program).Assembly)
.AddApplicationCommands()
.AddDbContext<SoundDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connStr))
    {
        string noDbConnStrMessage = "Database connection string not found in configuration. " +
            "Set it in user secrets, environment variables, or command-line args.";
        throw new InvalidOperationException(noDbConnStrMessage);
    }
    options.UseSqlite(connStr);
});

DbUtil.InitializeDatabase(connStr);

IHost host = builder.Build();

// Add commands from modules
host.AddModules(typeof(AbarCore).Assembly);

await host.RunAsync();
