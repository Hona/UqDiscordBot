using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Marten.Util;
using Microsoft.Extensions.Configuration;
using UqDiscordBot.Discord.Commands.Checks;
using UqDiscordBot.Discord.Models;
using UqDiscordBot.Discord.Services;

namespace UqDiscordBot.Discord.Commands.General
{
    [RequireBotChannel]
    public class CourseEnrolModule : UqModuleBase
    {
        public IConfiguration Configuration { get; set; }
        public CourseRaceConditionService CourseRaceConditionService { get; set; }

        private const Permissions StandardAccessPermissions = Permissions.AccessChannels | Permissions.ReadMessageHistory | Permissions.SendMessages;

        private DiscordChannel _matchingCourseChannel;
        private string _course;

        private async Task HandleInputAsync(CommandContext context, string course)
        {
            _matchingCourseChannel = null;

            // CSSE1001 - '1001'
            var courseNumber = course.Substring(course.Length - 4);
            if (!int.TryParse(courseNumber, out var _))
            {
                CourseRaceConditionService.SemaphoreSlim.Release();
                throw new UserException("Expected the course code to end in 4 numbers");
            }

            // Sanity trim
            _course = course.Trim().ToUpper();

            if (_course.Any(x => !char.IsLetterOrDigit(x)))
            {
                CourseRaceConditionService.SemaphoreSlim.Release();
                throw new Exception("Course codes must only contain alphanumeric characters");
            }

            var matchingChannel = context.Guild.Channels.Values
                .Where(x => x.Parent == null)
                .FirstOrDefault(x => string.Equals(x.Name, _course, StringComparison.OrdinalIgnoreCase));

            // Found it
            if (matchingChannel != default)
            {
                _matchingCourseChannel = matchingChannel;
            }
        }

        [Command("enroll")]
        [Aliases("enrol")]
        public async Task EnrolInCourseAsync(CommandContext context, string course)
            => await EnrolInCourseAsync(context, course, context.Member);

        [Command("enrollfor")]
        [Aliases("enrolfor")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task EnrolInCourseAsync(CommandContext context, string course, DiscordMember member)
        {
            await CourseRaceConditionService.SemaphoreSlim.WaitAsync();

            try
            {
                await HandleInputAsync(context, course);

                // Create it first if it doesn't exist
                if (_matchingCourseChannel == null)
                {
                    await context.RespondAsync("Creating channel for this course, you are the first person in it!");
                    _matchingCourseChannel = await context.Guild.CreateChannelAsync(_course, ChannelType.Text,
                        overwrites: new[]
                        {
                            new DiscordOverwriteBuilder()
                            {
                                Denied = StandardAccessPermissions
                            }.For(context.Guild.EveryoneRole)
                        });
                }

                // Add user to it
                await _matchingCourseChannel.AddOverwriteAsync(member, StandardAccessPermissions);

                await context.RespondAsync($"Added to course channel for {_matchingCourseChannel.Mention}");
            }
            catch
            {
                CourseRaceConditionService.SemaphoreSlim.Release();
                throw;
            }

            CourseRaceConditionService.SemaphoreSlim.Release();
        }

        [Command("mergebroken")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task CheckBrokenAsync(CommandContext context)
        {
            await CourseRaceConditionService.SemaphoreSlim.WaitAsync();

            try
            {
                var channels = context.Guild.Channels.Values.Where(x => x.Parent == null && !x.IsCategory).ToList();

                var outputtedChannelNames = new List<string>();

                foreach (var channel in channels)
                {
                    var channelName = new string(channel.Name.Where(char.IsLetterOrDigit).ToArray()).ToUpper();

                    if (outputtedChannelNames.Contains(channelName))
                    {
                        continue;
                    }

                    var matchingChannel = channels.FirstOrDefault(x =>
                        x.Id != channel.Id && new string(x.Name.Where(char.IsLetterOrDigit).ToArray()).ToUpper() ==
                        channelName);

                    if (matchingChannel == null)
                    {
                        continue;
                    }

                    outputtedChannelNames.Add(channelName);
                    await context.RespondAsync(channel.Mention + Environment.NewLine + matchingChannel.Mention);

                    foreach (var courseOverride in matchingChannel.PermissionOverwrites)
                    {
                        if (courseOverride.Type != OverwriteType.Member)
                        {
                            continue;
                        }

                        await channel.AddOverwriteAsync(await courseOverride.GetMemberAsync(), StandardAccessPermissions);
                    }

                    await matchingChannel.DeleteAsync();
                }
            }
            finally
            {
                CourseRaceConditionService.SemaphoreSlim.Release();
            }
        }

        [Command("drop")]
        public async Task DropCourseAsync(CommandContext context, string course)
        {
            await CourseRaceConditionService.SemaphoreSlim.WaitAsync();

            try
            {
                await HandleInputAsync(context, course);

                // Create it first if it doesn't exist
                if (_matchingCourseChannel == null)
                {
                    CourseRaceConditionService.SemaphoreSlim.Release();
                    await context.RespondAsync("That course does not exist");
                    return;
                }

                var permissions = _matchingCourseChannel.PermissionOverwrites.FirstOrDefault(x => x.Id == context.Member.Id);

                if (permissions == null)
                {
                    CourseRaceConditionService.SemaphoreSlim.Release();
                    await context.RespondAsync("You are not enrolled in that course");
                    return;
                }

                // Drop user from it
                await permissions.DeleteAsync();
                await context.RespondAsync($"Dropped course channel for {_matchingCourseChannel.Mention}");

                // If the only override is the default everyone role, then no members are left in channel, free to prune
                if (_matchingCourseChannel.PermissionOverwrites.All(x => x.Type != OverwriteType.Member))
                {
                    await _matchingCourseChannel.DeleteAsync("No members in class");
                }
            }
            catch
            {
                CourseRaceConditionService.SemaphoreSlim.Release();
                throw;
            }

            CourseRaceConditionService.SemaphoreSlim.Release();
        }

        [Command("info")]
        public async Task CourseInfoAsync(CommandContext context, string course)
        {
            await HandleInputAsync(context, course);

            if (_matchingCourseChannel == null)
            {
                await context.RespondAsync("No existing channel found for that course, create one with !enroll");
                return;
            }

            var embedBuilder = new DiscordEmbedBuilder()
            {
                Title = $"**{_course}**",
                Description = $"There are {_matchingCourseChannel.Users.Count(x => !x.IsBot)} people in the group chat"
            }.AddField("Jump", _matchingCourseChannel.Mention);

            await context.RespondAsync(embed: embedBuilder.Build());
        }

        [Command("count")]
        public async Task CourseTotalAsync(CommandContext context)
        {
            // Impossible string
            await HandleInputAsync(context, "aaaaaaaaaaaaAAAAAAAAAAAAAAAAAAAAAAAAAAAAaaaaaaaaaa0000");

            await context.RespondAsync($"Total of {context.Guild.Channels.Values.Count(x => x.Parent == null && !x.IsCategory)} course channels");
        }

        [Command("migrate")]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task MigrateChannelsAsync(CommandContext context)
        {
            await CourseRaceConditionService.SemaphoreSlim.WaitAsync();

            try
            {
                var categoryIds = Configuration.GetSection("Uq:CourseCategoryId").Get<ulong[]>();

                // Loop through each category searching for the channel name
                foreach (var categoryId in categoryIds)
                {
                    var category = await context.Client.GetChannelAsync(categoryId);

                    if (!category.IsCategory)
                    {
                        continue;
                    }

                    foreach (var child in category.Children)
                    {
                        await child.ModifyAsync(x =>
                        {
                            x.Parent = null;
                        });
                    }
                }
            }
            catch (Exception e)
            {
                CourseRaceConditionService.SemaphoreSlim.Release();
                throw;
            }

            CourseRaceConditionService.SemaphoreSlim.Release();
        }

        [Command("summary")]
        public async Task UserSummaryAsync(CommandContext context, DiscordMember member)
        {
            var courseChats = context.Guild.Channels.Values.Where(x => x.Parent == null && !x.IsCategory);

            var userChats = courseChats.Where(x => x.PermissionOverwrites.Any(x => x.Id == member.Id));

            await context.RespondAsync(embed: new DiscordEmbedBuilder
            {
                Title = "Course Summary",
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = member.DisplayName ?? member.Username,
                    IconUrl = member.AvatarUrl ?? member.DefaultAvatarUrl
                },
                Description = Formatter.BlockCode(string.Join(Environment.NewLine, userChats.Select(x => x.Name.ToUpper())))
            });
        }
    }
}