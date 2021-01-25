using DSharpPlus.CommandsNext.Attributes;

namespace UqDiscordBot.Discord.Commands.Checks
{
    public abstract class DescriptiveCheckBaseAttribute : CheckBaseAttribute
    {
        public string FailureResponse { get; set; }
    }
}