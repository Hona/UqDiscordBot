using System.Threading.Tasks;
using DSharpPlus.CommandsNext;

namespace UqDiscordBot.Discord.Commands.Checks
{
    public class RequireBotChannelAttribute : DescriptiveCheckBaseAttribute
    {
        private const ulong EnrolInCoursesChannelId = 803079176042446940;
        public RequireBotChannelAttribute()
        {
            FailureResponse = $"Please use the bot in the <#{EnrolInCoursesChannelId}> channel.";
        }
        
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help) 
            => Task.FromResult(ctx.Channel.Id == EnrolInCoursesChannelId);
    }
}