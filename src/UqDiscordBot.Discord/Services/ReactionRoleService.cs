﻿using System;
using System.Collections.Generic;
using System.Linq;
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
    public class ReactionRoleService
    {
        private readonly IConfiguration _config;

        private readonly DiscordClient _discordClient;

        // <RoleID, MessageID>
        private Dictionary<ulong, ulong> _existingRoleEmbeds;
        private DiscordChannel _textChannel;

        private ulong[] MentionRoles => _config.GetSection("ReactionRoles:RoleIds").Get<ulong[]>();
        private DiscordEmoji MentionEmoji => DiscordEmoji.FromName(_discordClient, _config["ReactionRoles:Emoji"]);

        public ReactionRoleService(DiscordClient discordClient, IConfiguration config)
        {
            _config = config;

            _discordClient = discordClient;
            _discordClient.GuildDownloadCompleted += _discordClient_GuildsDownloaded;
            _discordClient.MessageReactionAdded += _discordClient_MessageReactionAdded;
            _discordClient.MessageReactionRemoved += _discordClient_MessageReactionRemoved;
        }

        private Task _discordClient_GuildsDownloaded(DiscordClient sender, GuildDownloadCompletedEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                _textChannel = await _discordClient.GetChannelAsync(ulong.Parse(_config["ReactionRoles:ChannelId"]));

                await LoadExistingRoleEmbedsAsync();
                await SendRoleEmbedsAsync();

                await VerifyCurrentUserRolesAsync();
            });

            return Task.CompletedTask;
        }

        private async Task LoadExistingRoleEmbedsAsync()
        {
            _existingRoleEmbeds = new Dictionary<ulong, ulong>();

            // Filter only messages from this bot
            var existingMessages = (await _textChannel.GetMessagesAsync()).FromSelf(_discordClient);

            foreach (var message in existingMessages)
            {
                var (result, role) = await TryParseRoleFromEmbedAsync(message);
                if (result)
                {
                    _existingRoleEmbeds.Add(role.Id, message.Id);
                }
            }
        }

        private async Task SendRoleEmbedsAsync()
        {
            // If there are roles added to the config, but aren't sent yet, send them
            if (MentionRoles != null)
            {
                foreach (var mentionRole in MentionRoles)
                {
                    if (_existingRoleEmbeds.ContainsKey(mentionRole))
                    {
                        continue;
                    }

                    var role = _textChannel.Guild.Roles.First(x => x.Key == mentionRole);
                    await SendRoleEmbed(role.Value);
                }
            }
        }

        private async Task VerifyCurrentUserRolesAsync()
        {
            var members = (await _textChannel.Guild.GetAllMembersAsync()).ToList();

            var usersWithMentionRoles =
                members.Where(x => MentionRoles.Intersect(x.Roles.Select(y => y.Id)).Any()).ToList();

            // Check users who have reacted to the embed
            foreach (var (roleId, messageId) in _existingRoleEmbeds)
            {
                var message = await _textChannel.GetMessageAsync(messageId);
                var role = _textChannel.Guild.GetRole(roleId);

                if (!message.Author.IsSelf(_discordClient))
                {
                    continue;
                }

                // Get all users who have reacted to the embed
                var reactionUsers =
                    (await message.GetReactionsAsync(MentionEmoji, _textChannel.Guild.MemberCount))
                    .ToList();

                foreach (var user in reactionUsers.Where(user => !user.IsSelf(_discordClient)
                                                                 && !usersWithMentionRoles.Any(x =>
                                                                     x.Roles.Any(y => y.Id == roleId) &&
                                                                     x.Id == user.Id)))
                {
                    var member = members.FirstOrDefault(x => x.Id == user.Id);

                    if (member == null)
                    {
                        // User doesn't exist in the guild lets delete their reaction
                        continue;
                    }

                    // Make sure the user is not null, in case they have been banned/left the server
                    await member.GrantRoleAsync(role);
                }

                var userWithRole = usersWithMentionRoles.Where(x => x.Roles.Any(x => x.Id == roleId));
                foreach (var member in userWithRole)
                {
                    if (reactionUsers.Any(x => x.Id == member.Id) && !member.IsSelf(_discordClient))
                    {
                        continue;
                    }

                    // User has not reacted, remove the role
                    var guildUser = await _textChannel.Guild.GetMemberAsync(member.Id);
                    await guildUser.RevokeRoleAsync(role);
                }
            }
        }

        /// <summary>
        ///     The role should be @Mentioned in the Description of the Embed
        /// </summary>
        private async Task<(bool Result, DiscordRole Role)> TryParseRoleFromEmbedAsync(DiscordMessage input)
        {
            var message = await input.Channel.GetMessageAsync(input.Id);

            if (message.Embeds.Count == 1)
            {
                var embed = message.Embeds.First();
                var guild = _textChannel.Guild;

                // No library provided way to parse the role, the mention should never change, as it uses the ID
                var role = guild.Roles.Values.FirstOrDefault(x =>
                    embed.Description.Contains(x.Mention, StringComparison.InvariantCultureIgnoreCase));
                var result = role != null;
                return (result, role);
            }

            return (false, null);
        }

        private Task _discordClient_MessageReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                if (e.Guild == null || e.Emoji == null || _textChannel == null || e.Channel.Id != _textChannel.Id ||
                    e.Emoji.Id != MentionEmoji.Id)
                {
                    return;
                }

                // Check that the message reacted to is a role embed
                if (_existingRoleEmbeds.ContainsValue(e.Message.Id))
                {
                    if (e.User is DiscordMember member)
                    {
                        // Ignore actions from the bot
                        if (member.IsSelf(_discordClient))
                        {
                            return;
                        }

                        var (result, role) = await TryParseRoleFromEmbedAsync(e.Message);
                        if (result)
                        {
                            await member.GrantRoleAsync(role);
                        }
                    }
                }
            });

            return Task.CompletedTask;
        }

        private Task _discordClient_MessageReactionRemoved(DiscordClient sender, MessageReactionRemoveEventArgs e)
        {
            _ = Task.Run(async () =>
            {
                if (e.Guild == null && e.Emoji == null || _textChannel == null || e.Channel.Id != _textChannel.Id ||
                    e.Emoji != MentionEmoji)
                {
                    return;
                }

                // Check that the message reacted to is a role embed
                if (_existingRoleEmbeds.ContainsValue(e.Message.Id))
                {
                    if (e.User is DiscordMember member)
                    {
                        // Ignore actions from the bot
                        if (member.IsSelf(_discordClient))
                        {
                            return;
                        }

                        var (result, role) = await TryParseRoleFromEmbedAsync(e.Message);
                        if (result)
                        {
                            await member.RevokeRoleAsync(role);
                        }
                    }
                }
            });

            return Task.CompletedTask;
        }

        public async Task SendRoleEmbed(DiscordRole role)
        {
            // If the role isn't already sent
            if (!_existingRoleEmbeds.ContainsKey(role.Id))
            {
                var embed = new DiscordEmbedBuilder
                {
                    Description = role.Mention,
                    Color = role.Color
                }.Build();

                var message = await _textChannel.SendMessageAsync(embed: embed);
                await message.CreateReactionAsync(MentionEmoji);

                _existingRoleEmbeds.Add(role.Id, message.Id);
            }
        }

        public async Task RemoveRoleEmbed(DiscordRole role)
        {
            // If the role is sent
            if (_existingRoleEmbeds.TryGetValue(role.Id, out var messageId))
            {
                var message = await _textChannel.GetMessageAsync(messageId);
                await message.DeleteAsync();

                _existingRoleEmbeds.Remove(role.Id);
            }
        }
    }
}