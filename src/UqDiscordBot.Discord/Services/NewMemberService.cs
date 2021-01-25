using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using UqDiscordBot.Discord.Helpers;
using UqDiscordBot.Discord.Models;

namespace UqDiscordBot.Discord.Services
{
    [BotService(BotServiceType.InjectAndInitialize)]
    public class NewMemberService
    {
        private readonly IConfiguration _config;
        private readonly DiscordClient _discordClient;
        private readonly SemaphoreSlim _semaphoreLock = new(1, 1);
        private List<ulong> _faqLockInformed = new();
        private DiscordMessage _lastMessage;

        private DiscordChannel _textChannel;
        private DiscordEmoji _newMembersEmoji;
        private DiscordRole _newMembersRole;
        
        public NewMemberService(DiscordClient discordClient, IConfiguration config)
        {
            _config = config;

            _discordClient = discordClient;
            
            _discordClient.GuildDownloadCompleted += _discordClient_GuildsDownloaded;
            _discordClient.MessageReactionAdded += _discordClient_MessageReactionAdded;
        }

        public bool IsEnabled { get; private set; } = true;

        public void Lock()
        {
            IsEnabled = false;
        }

        public async Task UnlockAsync()
        {
            IsEnabled = true;
            await HookToLastMessageAsync();
            await AddUnhandedReactionRolesAsync();
            _faqLockInformed = new List<ulong>();
        }

        private async Task RemoveAllReactionsAsync(DiscordChannel textChannel)
        {
            if (textChannel != null)
            {
                var messages = (await textChannel.GetMessagesAsync()).ToList();

                _lastMessage = messages.OrderByDescending(x => x.Timestamp.Ticks).FirstOrDefault();

                // Remove all existing reactions
                foreach (var message in messages)
                {
                    // If the previous message hooked to, is the same as what is the new last message, don't remove all reactions
                    // This is to prevent unhandled reactions being wiped
                    if (!message.IsUserMessage() || _lastMessage != null && message.Id == _lastMessage.Id)
                    {
                        continue;
                    }

                    if (message.Reactions.Count > 0)
                    {
                        await message.DeleteAllReactionsAsync();
                    }
                }
            }
        }

        public async Task HookToLastMessageAsync()
        {
            await _semaphoreLock.WaitAsync();

            var channel = _discordClient.FindChannel(ulong.Parse(_config["NewMembers:ChannelId"]));
            
            if (channel.Type == ChannelType.Text)
            {
                if (_textChannel != null && channel.Id != _textChannel.Id)
                {
                    // If there is a message hooked before, make sure to remove the reaction
                    await RemoveAllReactionsAsync(_textChannel);
                }

                _textChannel = channel;

                await RemoveAllReactionsAsync(_textChannel);

                if (_lastMessage.IsUserMessage())
                {
                    try
                    {
                        await _lastMessage.CreateReactionAsync(_newMembersEmoji);
                    }
                    catch (Exception e)
                    {
                        // TODO: Rethink this
                        Console.WriteLine(e);
                        _semaphoreLock.Release();
                        throw;
                    }
                }
            }

            _semaphoreLock.Release();
        }

        private Task _discordClient_GuildsDownloaded(DiscordClient sender, GuildDownloadCompletedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                _newMembersEmoji = DiscordEmoji.FromName(_discordClient, _config["NewMembers:Emoji"]);
                _newMembersRole = _discordClient.FindRole(ulong.Parse(_config["NewMembers:RoleId"]));
                
                await HookToLastMessageAsync();
                await AddUnhandedReactionRolesAsync();
            });

            return Task.CompletedTask;
        }

        private Task _discordClient_MessageReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                await _semaphoreLock.WaitAsync();
                _semaphoreLock.Release();

                if (e.Channel.Id != _textChannel.Id || e.Emoji.Id != _newMembersEmoji.Id ||
                    !(e.User is DiscordMember member))
                {
                    return;
                }

                if (!IsEnabled)
                {
                    if (_faqLockInformed.Contains(member.Id) || member.Roles.All(x => x.Id != _newMembersRole.Id))
                    {
                        return;
                    }

                    // Only send the DM once.
                    _faqLockInformed.Add(member.Id);

                    await member.SendMessageAsync(embed: new DiscordEmbedBuilder
                    {
                        Description =
                            "The FAQ verification process is temporarily locked, this is most likely due to spam bots joining. Please try again later.",
                        Color = DiscordColor.Orange
                    }.Build());

                    return;
                }

                // Check that the message reacted to is the last message in the channel
                if (_lastMessage.Id == e.Message.Id)
                {
                    // Ignore actions from the bot, or if the user already has the role
                    if (!member.IsSelf(_discordClient))
                    {
                        if (member.Roles.Any(x => x.Id == _newMembersRole.Id))
                        {
                            await member.RevokeRoleAsync(_newMembersRole);
                        }

                        if (_lastMessage.IsUserMessage())
                        {
                            await _lastMessage.DeleteReactionAsync(_newMembersEmoji, member);
                        }
                    }
                }
            });

            return Task.CompletedTask;
        }

        public async Task AddUnhandedReactionRolesAsync()
        {
            await _semaphoreLock.WaitAsync();

            // Get all user reactions with the FAQ emoji, max users is the guild member count
            var userReactions =
                (await _lastMessage.GetReactionsAsync(_newMembersEmoji, _textChannel.Guild.MemberCount))
                .Where(x => !x.IsSelf(_discordClient));

            foreach (var unhandledUserReaction in userReactions)
            {
                var member = await _textChannel.Guild.GetMemberAsync(unhandledUserReaction.Id);
                if (member != null && member.Roles.Any(x => x.Id == _newMembersRole.Id))
                {
                    await member.RevokeRoleAsync(_newMembersRole);
                }

                await _lastMessage.DeleteReactionAsync(_newMembersEmoji, unhandledUserReaction);
            }

            _semaphoreLock.Release();
        }
    }
}