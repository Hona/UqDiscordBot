using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.Extensions.Logging;
using UqDiscordBot.Discord.Services;

namespace UqDiscordBot.Discord.Commands
{
    [RequireUserPermissions(Permissions.Administrator)]
    public class AdminModule : UqModuleBase
    {
        
    }
}