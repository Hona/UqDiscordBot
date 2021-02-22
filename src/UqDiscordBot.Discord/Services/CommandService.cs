using System;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UqDiscordBot.Discord.Helpers;
using UqDiscordBot.Discord.Models;

namespace UqDiscordBot.Discord.Services
{
    [BotService(BotServiceType.InjectAndInitialize)]
    public class CommandService
    {
        private DiscordChannel _discordChannel;
        private DiscordClient _discordClient;
        private async Task<DiscordChannel> GetLogChannelAsync()
        {
            if (_discordChannel != null)
            {
                return _discordChannel;
            }

            _discordChannel = await _discordClient.GetChannelAsync(803079434973216811);
            return _discordChannel;
        }
        public CommandService(IConfiguration config, DiscordClient discordClient, IServiceProvider services)
        {
            _discordClient = discordClient;

            var commands = discordClient.UseCommandsNext(new CommandsNextConfiguration
            {
                StringPrefixes = new[]
                {
                    config["Discord:CommandPrefix"]
                },
                EnableDms = true,
                CaseSensitive = false,
                EnableMentionPrefix = true,
                Services = services
            });

            discordClient.UseInteractivity(new InteractivityConfiguration()
            {
                PollBehaviour = PollBehaviour.KeepEmojis,
                Timeout = TimeSpan.FromSeconds(60)
            });

            commands.RegisterCommands(Assembly.GetEntryAssembly());

            commands.CommandErrored += commands_CommandErrored;
        }

        private Task commands_CommandErrored(CommandsNextExtension sender, CommandErrorEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                var logChannel = await GetLogChannelAsync();

                if (e.Exception is ChecksFailedException exception)
                {
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Access Denied",
                        Description = exception.FailedChecks.ToCleanResponse(),
                        Color = DiscordColor.Red
                    };

                    await e.Context.RespondAsync(embed: embed);
                    await logChannel.SendMessageAsync(exception.FailedChecks.ToCleanResponse(), embed: new DiscordEmbedBuilder
                    {
                        Title = "Exception",
                        Description = exception.ToString()
                    });
                }
                else if (e.Exception is UserException userException)
                {
                    var embed = new DiscordEmbedBuilder
                    {
                        Title = "Error",
                        Description = userException.Message,
                        Color = DiscordColor.Red
                    };

                    await e.Context.RespondAsync(embed: embed);
                    await logChannel.SendMessageAsync(embed: new DiscordEmbedBuilder
                    {
                        Title = "Exception",
                        Description = userException.ToString()
                    });
                }

                // No need to log when a command isn't found
                else if (!(e.Exception is CommandNotFoundException))
                {
                    e.Context.Client.Logger.LogError(e.Exception,
                        $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}",
                        DateTime.Now);

                    await e.Context.RespondAsync(embed: new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Red,
                        Title = "Something went wrong",
                        Description = e.Exception.Message
                    });

                    await logChannel.SendMessageAsync(embed: new DiscordEmbedBuilder
                    {
                        Title = "Exception",
                        Description = e.Exception.ToString()
                    });
                }
            });

            return Task.CompletedTask;
        }
    }
}