using Lavalink4NET.Extensions;
using Lavalink4NET.InactivityTracking.Trackers.Idle;
using Lavalink4NET.InactivityTracking.Trackers.Users;
using Lavalink4NET.NetCord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Abar.Bot.Services;
using NetCord.Abar.Bot.Services.Interfaces;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

var config = new ConfigurationManager();

config
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Configuration = config
});

string? abarConnStr = builder.Configuration.GetConnectionString("AbarBot");
if (string.IsNullOrWhiteSpace(abarConnStr))
{
    string noDbConnStrMessage = "Database connection string not found in configuration. " +
        "Set it in user secrets, environment variables, or command-line args.";
    throw new InvalidOperationException(noDbConnStrMessage);
}

builder.Services
    .AddDiscordGateway()
    .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>()
    .AddComponentInteractions<StringMenuInteraction, StringMenuInteractionContext>()
    .AddComponentInteractions<UserMenuInteraction, UserMenuInteractionContext>()
    .AddComponentInteractions<RoleMenuInteraction, RoleMenuInteractionContext>()
    .AddComponentInteractions<MentionableMenuInteraction, MentionableMenuInteractionContext>()
    .AddComponentInteractions<ChannelMenuInteraction, ChannelMenuInteractionContext>()
    .AddComponentInteractions<ModalInteraction, ModalInteractionContext>();

// reads appsettings, env vars, user-secrets, CLI
string? token = builder.Configuration["Discord:Token"];
if (string.IsNullOrWhiteSpace(token))
{
    string noDiscordTokenMessage = "Discord token not found in configuration. " +
        "Set it in user secrets, environment variables, or command-line args.";
    throw new InvalidOperationException(noDiscordTokenMessage);
}

string? soundsDir = builder.Configuration["SoundsDirectory"];
if (string.IsNullOrWhiteSpace(soundsDir))
{
    throw new InvalidOperationException("sounds directory cannot be null");
}

// Database connection string goes here.
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
.AddSingleton<ITrackSearchService>(sp => new TrackSearchService(
    soundRoot: @"C:/Users/jorda/source/repos/NetCord.Abar.Bot/AbarBot/Sounds"
))
.AddApplicationCommands()
.AddDbContext<AbarBotDbContext>(options => 
{
    options.UseSqlite(abarConnStr);
})
.AddSingleton<IVoiceService, VoiceService>();

IHost host = builder.Build()
    .AddModules(typeof(Program).Assembly);

Console.WriteLine("Using DB at: " + Path.GetFullPath("./abar_bot.db"));

await host.RunAsync();
