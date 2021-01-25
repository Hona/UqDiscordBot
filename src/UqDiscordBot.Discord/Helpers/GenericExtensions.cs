using System;
using System.ComponentModel;

namespace UqDiscordBot.Discord.Helpers
{
    public static class GenericExtensions
    {
        public static T Convert<T>(this string input)
        {
            try
            {
                var converter = TypeDescriptor.GetConverter(typeof(T));
                // Cast ConvertFromString(string text) : object to (T)
                return (T)converter.ConvertFromString(input);
            }
            catch (NotSupportedException)
            {
                return default;
            }
        }
    }
}