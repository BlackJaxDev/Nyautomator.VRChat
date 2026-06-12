using Newtonsoft.Json;
using OscCore;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace Nyautomator
{
    /// <summary>
    /// Shared VRChat helper namespace for OSC text command support.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that exposes and parses text commands for avatar parameters.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Collects text command suggestions for built-in and loaded avatar parameters.
            /// </summary>
            /// <returns>Command strings in the form <c>osc Name = &lt;Type&gt;</c>.</returns>
            public static IEnumerable<string> CollectCommands()
            {
                if (!Sending || !IsVRChatRunning())
                    return [];

                List<string> commands = [];
                commands.AddRange(BuiltInParameterNames.Select(x => $"osc {x.Key} = <{x.Value.Type}>"));

                if (Config?.Parameters is not null)
                    commands.AddRange(Config.Parameters.Where(x => x.Name is not null && (x.Input?.Type is not null || x.Output?.Type is not null)).Select(p => $"osc {p.Name!} = <{(p.Input?.Type ?? p.Output?.Type)!}>"));

                return commands;
            }

            /// <summary>
            /// Parses a raw parameter assignment command and sends the value when it can be typed.
            /// </summary>
            /// <param name="input">Command text containing a parameter name and value separated by equals.</param>
            /// <returns><see langword="true"/> when the parameter value was parsed and sent.</returns>
            public static bool HandleCommand(string? input)
            {
                if (input is null)
                    return false;

                var parts = input.Split('=');
                if (parts.Length < 2)
                    return false;

                string parameterName = parts[0].Trim();
                string? type = BuiltInParameterNames.TryGetValue(parameterName, out (string Type, string Description) builtinParameter)
                    ? builtinParameter.Type 
                    : Config?.Parameters?.FirstOrDefault(x => x.Name?.Equals(parameterName) ?? false)?.Input?.Type ?? Config?.Parameters?.FirstOrDefault(x => x.Name?.Equals(parameterName) ?? false)?.Output?.Type ?? ParseType(parts[1]);

                return SetValue(parameterName, parts[1].Trim(), type);
            }

            /// <summary>
            /// Infers a VRChat OSC primitive type from raw text.
            /// </summary>
            /// <param name="value">Raw value text to inspect.</param>
            /// <returns>Bool, Int, Float, or <see langword="null"/> when no supported type is inferred.</returns>
            private static string? ParseType(string value)
            {
                if (bool.TryParse(value, out _))
                    return Bool;
                if (int.TryParse(value, out _))
                    return Int;
                if (float.TryParse(value, out _))
                    return Float;
                return null;
            }

            /// <summary>
            /// Parses a value according to a VRChat parameter type and sends it to the parameter.
            /// </summary>
            /// <param name="parameterName">Parameter name to update.</param>
            /// <param name="value">Raw value text.</param>
            /// <param name="type">VRChat parameter type to parse as.</param>
            /// <returns><see langword="true"/> when parsing succeeded and a value was sent.</returns>
            public static bool SetValue(string parameterName, string value, string? type)
            {
                switch (type)
                {
                    case Int:
                        if (int.TryParse(value, out int intValue))
                        {
                            Send(parameterName, intValue);
                            return true;
                        }
                        else
                            Console.WriteLine($"Failed to parse value '{value}' as an int");
                        break;
                    case Bool:
                        if (bool.TryParse(value, out bool boolValue))
                        {
                            Send(parameterName, boolValue);
                            return true;
                        }
                        else
                            Console.WriteLine($"Failed to parse value '{value}' as a bool");
                        break;
                    case Float:
                        if (float.TryParse(value, out float floatValue))
                        {
                            Send(parameterName, floatValue);
                            return true;
                        }
                        else
                            Console.WriteLine($"Failed to parse value '{value}' as a float");
                        break;
                }
                return false;
            }

            /// <summary>
            /// Parses a chat-style <c>!osc</c> command and sends the inferred avatar parameter value.
            /// </summary>
            /// <param name="msg">Incoming command message text.</param>
            private static void ParseCommandMessage(string msg)
            {
                //Find the start of the osc command in the message
                int oscIndex = msg.IndexOf("!osc", StringComparison.InvariantCultureIgnoreCase);
                if (oscIndex >= 0)
                    msg = msg[(oscIndex + 4)..];

                //Find the splitter between the parameter name and value
                int equalsIndex = msg.IndexOf('=');
                if (equalsIndex < 0)
                    return;

                string parameterName = msg[..equalsIndex].Trim();
                string value = msg[(equalsIndex + 1)..].Trim();

                //Remove anything in the command after the value
                int whitespaceIndex = value.IndexOf(' ');
                if (whitespaceIndex >= 0)
                    value = value[..whitespaceIndex];

                value = value.Trim();
                if (value.Contains('.'))
                {
                    if (float.TryParse(value, out float floatValue))
                        Send(parameterName, floatValue);
                }
                else if (int.TryParse(value, out int intValue))
                    Send(parameterName, intValue);
                else if (bool.TryParse(value, out bool boolValue))
                    Send(parameterName, boolValue);
            }
        }
    }
}
