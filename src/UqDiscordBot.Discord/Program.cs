using System.Reflection;
using DSharpPlus;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using UqDiscordBot.Discord;
using UqDiscordBot.Discord.Helpers;
using Weasel.Postgresql;

using var logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

var logFactory = new LoggerFactory().AddSerilog(logger);

var hostBuilder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddSingleton(new DiscordClient(new DiscordConfiguration
        {
            Token = hostContext.Configuration["Discord:Token"],
            TokenType = TokenType.Bot,
            AutoReconnect = true,
            MinimumLogLevel = LogLevel.None,
            LoggerFactory = logFactory,
            MessageCacheSize = 512,
            Intents = DiscordIntents.All
        }));

        services.InjectBotServices(Assembly.GetEntryAssembly());

        services.AddMarten(options =>
        {
            options.Connection(hostContext.Configuration.GetConnectionString("Marten"));
            
            options.AutoCreateSchemaObjects = AutoCreate.All;
        });

        services.AddHostedService<Bot>();
    });

var host = hostBuilder.Build();

host.Services.InitializeMicroservices(Assembly.GetEntryAssembly());

host.Run();