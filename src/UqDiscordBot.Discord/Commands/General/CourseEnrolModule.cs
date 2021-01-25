using System;
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
            var categoryId = Configuration["Uq:CourseCategoryId"];
            _category = await context.Client.GetChannelAsync(ulong.Parse(categoryId));

            if (!_category.IsCategory)
            {
                throw new UserException("Bot error, please contact Hona - Category channel is not a category!");
            }

            var courseChannels = _category.Children.ToList();

            _matchingCourseChannel = courseChannels.FirstOrDefault(x => string.Equals(x.Name, course, StringComparison.OrdinalIgnoreCase));
        }

        [Command("enrol")]
        public async Task EnrolInCourseAsync(CommandContext context, string course)
        {
            await HandleInputAsync(context, course);

            // Create it first if it doesn't exist
            if (_matchingCourseChannel == null)
            {
                _matchingCourseChannel = await context.Guild.CreateChannelAsync(_course, ChannelType.Text, _category);
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

            var permissions = _matchingCourseChannel.PermissionsFor(context.Member);
            if ((permissions & StandardAccessPermissions) != StandardAccessPermissions)
            {
                await context.RespondAsync("You are not enrolled in that course");
                return;
            }
            
            // Drop user from it
            await _matchingCourseChannel.AddOverwriteAsync(context.Member, allow: Permissions.None, deny: Permissions.AccessChannels | Permissions.ReadMessageHistory | Permissions.SendMessages);

            await context.RespondAsync($"Dropped course channel for {_matchingCourseChannel.Mention}");
        }
    }
}