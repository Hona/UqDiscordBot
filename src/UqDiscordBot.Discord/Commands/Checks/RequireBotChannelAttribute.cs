using System.Threading.Tasks;
using DSharpPlus.CommandsNext;

namespace UqDiscordBot.Discord.Commands.Checks
{
    public class RequireBotChannelAttribute : DescriptiveCheckBaseAttribute
    {
        private const ulong EnrolInCoursesChannelId = 803079176042446940;
        private const ulong BotAdminChannelId = 803079434973216811;
        public RequireBotChannelAttribute()
        {
            FailureResponse = $"Please use the bot in the <#{EnrolInCoursesChannelId}> channel.";
        }
        
        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help) 
            => Task.FromResult(ctx.Channel.Id == EnrolInCoursesChannelId || ctx.Channel.Id == BotAdminChannelId);
    }
}