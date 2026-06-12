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
    /// Shared VRChat helper namespace for chatbox OSC operations.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that sends VRChat chatbox messages and typing state.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Sends a normalized VRChat chatbox message over OSC when sending is active.
            /// </summary>
            /// <param name="message">Message text to send.</param>
            /// <param name="sendImmediately">Whether VRChat should submit the message immediately.</param>
            /// <param name="playSound">Whether VRChat should play the chatbox sound.</param>
            public static void SendChatboxMessage(string? message, bool sendImmediately = true, bool playSound = true)
            {
                if (!Sending)
                    return;

                var payload = NormalizeChatboxMessage(message ?? string.Empty);
                SendOscMessage(new OscMessage("/chatbox/input", payload, sendImmediately, playSound));
            }

            /// <summary>
            /// Sends the VRChat chatbox typing indicator state over OSC when sending is active.
            /// </summary>
            /// <param name="isTyping">Typing indicator value to send.</param>
            public static void SetChatboxTyping(bool isTyping)
            {
                if (!Sending)
                    return;

                SendOscMessage(new OscMessage("/chatbox/typing", isTyping));
            }

            /// <summary>
            /// Normalizes chatbox text to VRChat's line and character limits.
            /// </summary>
            /// <param name="message">Raw message text.</param>
            /// <returns>Message text limited to nine lines and 144 characters.</returns>
            private static string NormalizeChatboxMessage(string message)
            {
                if (string.IsNullOrEmpty(message))
                    return string.Empty;

                var normalized = message.Replace("\r\n", "\n").Replace('\r', '\n');
                var segments = normalized.Split('\n');
                StringBuilder builder = new(normalized.Length);
                int linesUsed = 0;

                foreach (var segment in segments)
                {
                    if (linesUsed >= 9 || builder.Length >= 144)
                        break;

                    if (linesUsed > 0)
                    {
                        if (builder.Length + 1 > 144)
                            break;
                        builder.Append('\n');
                    }

                    var remaining = 144 - builder.Length;
                    var content = segment;
                    if (content.Length > remaining)
                        content = content[..remaining];

                    builder.Append(content);
                    linesUsed++;
                }

                return builder.ToString();
            }
        }
    }
}
