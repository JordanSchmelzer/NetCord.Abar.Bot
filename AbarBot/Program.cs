using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;

using Microsoft.EntityFrameworkCore;

using NetCord.Abar.Bot.Commands;
using NetCord.Abar.Bot.Database;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Services.Interfaces;

//using Lavalink4NET.NetCord;
using Lavalink4NET.Extensions;
using Lavalink4NET.NetCord;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;


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
DbUtil.InitializeDatabase(connStr);

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
.AddLavalink()
.ConfigureLavalink(config =>
{
    // The address of the Lavalink node.
    config.BaseAddress = new Uri("http://localhost:2333");
    // The URI that is used to connect to the Lavalink node.
    // config.WebSocketUri = new Uri("ws://localhost:2333/v4/websocket");
    // Time Lavalink4Net waits for the Lavalink node to become ready.
    config.ReadyTimeout = TimeSpan.FromSeconds(10);
    // Used to identify the Lavalink node in logs.
    config.Label = "AbarLavalink";
    // The password used to connect to the Lavalink node.
    config.Passphrase = "youshallnotpass";
    // The name of the HttpClient that is used to connect to the Lavalink node.
    config.HttpClientName = "AbarLavalinkHttpClient";
})
.Configure<IdleInactivityTrackerOptions>(config =>
{
    // Specify the timeout after which the player is reported as inactive.
    //config.Timeout = TimeSpan.FromSeconds(300); // 5 minutes (default)
    // Identify the tracker
    config.Label = "AbarIdleTracker";
    // specify the states taht are considered idle
    // default is PlayerState.Paused and PlayerState.NotPlaying
    // config.IdleStates = PlayerState.Paused | PlayerState.NotPlaying;
})
.Configure<UsersInactivityTrackerOptions>(config =>
{
    // Specify the timeout after which the player is reported as inactive.
    // default is whatever IdleInactivityTrackerOptions.Timeout is set to
    //config.Timeout = TimeSpan.FromSeconds(300);
    // Identify the tracker
    config.Label = "AbarUsersTracker";
    // specify the states taht are considered idle
    // used to specify the threshold indicating how many users must be in teh voice channel
    // to report the player as inactive. The default is 1.
    // Seting to below 1 wil treat all players as active.
    config.Threshold = 1;
    // specify if bots should be excluded from the user count. The default is true.
    //config.ExcludeBots = true;
})
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
})
.AddSingleton<IVoiceService, VoiceService>();

IHost host = builder.Build()
    .AddModules(typeof(AbarCore).Assembly);

await host.RunAsync();
