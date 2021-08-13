using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using UqDiscordBot.Discord.Models;

namespace UqDiscordBot.Discord.Services
{
    [BotService(BotServiceType.InjectAndInitialize)]
    public class ConfessionService
    {
        private readonly DiscordClient _discordClient;
        private readonly IConfiguration _configuration;
        private readonly Random _random = new();
        
        private DiscordChannel _confessionChannel;
        private DiscordChannel _adminChannel;

        public ConfessionService(DiscordClient discordClient, IConfiguration configuration)
        {
            _discordClient = discordClient;
            _configuration = configuration;

            _discordClient.MessageCreated += DiscordClientOnMessageCreated;
            
            _discordClient.GuildDownloadCompleted += DiscordClientOnGuildDownloadCompleted;
        }

        private Task DiscordClientOnGuildDownloadCompleted(DiscordClient sender, GuildDownloadCompletedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await sender.UpdateStatusAsync(new DiscordActivity("DM for anonymous confessions/love letters"));
            });
            
            return Task.CompletedTask;
        }

        private Task DiscordClientOnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
        {
            if (e.Channel is not DiscordDmChannel)
            {
                return Task.CompletedTask;
            }
            
            _ = Task.Run(async () =>
            {
                _confessionChannel ??= await _discordClient.GetChannelAsync(ulong.Parse(_configuration["ConfessionChannelId"]));
                
                _adminChannel ??= await _discordClient.GetChannelAsync(ulong.Parse(_configuration["AdminChannelId"]));

                var embedBuilder = new DiscordEmbedBuilder
                {
                    Title = "Confession",
                    Color = new DiscordColor((byte)_random.Next(256), _random.Next(256), _random.Next(256)),
                    Description = e.Message.Content,
                    Timestamp = DateTimeOffset.Now
                };
                
                var confessionMessage = await _confessionChannel.SendMessageAsync(embed: embedBuilder.Build());

                var adminEmbedBuilder = new DiscordEmbedBuilder
                {
                    Title = "Confession Log",
                    Author = new DiscordEmbedBuilder.EmbedAuthor
                    {
                        Name = e.Author.Username,
                        IconUrl = e.Author.AvatarUrl ?? e.Author.DefaultAvatarUrl
                    },
                    Description = Formatter.MaskedUrl("Jump to Message", confessionMessage.JumpLink)
                };
                
                await _adminChannel.SendMessageAsync(embed: adminEmbedBuilder.Build());
            });

            return Task.CompletedTask;
        }
    }
}