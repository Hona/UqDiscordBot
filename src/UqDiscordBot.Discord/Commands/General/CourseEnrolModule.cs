using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Configuration;
using UqDiscordBot.Discord.Commands.Checks;
using UqDiscordBot.Discord.Models;

namespace UqDiscordBot.Discord.Commands.General
{
    [RequireBotChannel]
    public class CourseEnrolModule : UqModuleBase
    {
        public IConfiguration Configuration { get; set; }

        private const Permissions StandardAccessPermissions = Permissions.AccessChannels | Permissions.ReadMessageHistory | Permissions.SendMessages;

        private DiscordChannel _matchingCourseChannel;
        private DiscordChannel _category;
        private string _course;

        private List<DiscordChannel> _allCourseCategories = new();
        
        private async Task HandleInputAsync(CommandContext context, string course)
        {
            // CSSE1001 - '1001'
            var courseNumber = course.Substring(course.Length - 4);
            if (!int.TryParse(courseNumber, out var _))
            {
                throw new UserException("Expected the course code to end in 4 numbers");
            }

            // Sanity trim
            _course = course.Trim().ToUpper();
            
            // Check if it already exists
            var categoryIds = Configuration.GetSection("Uq:CourseCategoryId").Get<ulong[]>();


            foreach (var categoryId in categoryIds)
            {
                var category = await context.Client.GetChannelAsync(categoryId);
                
                if (!category.IsCategory)
                {
                    continue;
                }
                
                _allCourseCategories.Add(category);

                var courseChannels = category.Children.ToList();

                var matchingChannel = courseChannels.FirstOrDefault(x => string.Equals(x.Name, course, StringComparison.OrdinalIgnoreCase));

                if (matchingChannel != null)
                {
                    _matchingCourseChannel = matchingChannel
                }
            }
        }

        [Command("enroll")]
        public async Task EnrolInCourseAsync(CommandContext context, string course)
        {
            await HandleInputAsync(context, course);

            // Create it first if it doesn't exist
            if (_matchingCourseChannel == null)
            {
                var courseCategory = _allCourseCategories.FirstOrDefault(x => x.Children.Count() < 50);

                if (courseCategory == null)
                {
                    throw new UserException("Out of course categories, contact Hona to make more");
                }
                
                _matchingCourseChannel = await context.Guild.CreateChannelAsync(_course, ChannelType.Text, courseCategory);
            }
            
            // Add user to it
            await _matchingCourseChannel.AddOverwriteAsync(context.Member, StandardAccessPermissions);

            await context.RespondAsync($"Added to course channel for {_matchingCourseChannel.Mention}");
        }
        
        [Command("drop")]
        public async Task DropCourseAsync(CommandContext context, string course)
        {
            await HandleInputAsync(context, course);

            // Create it first if it doesn't exist
            if (_matchingCourseChannel == null)
            {
                await context.RespondAsync("That course does not exist");
                return;
            }

            var permissions = _matchingCourseChannel.PermissionOverwrites.FirstOrDefault(x => x.Id == context.Member.Id);

            if (permissions == null)
            {
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
    }
}