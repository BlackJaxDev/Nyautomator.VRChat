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
    /// Shared VRChat helper namespace for OSC message conversion helpers.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper containing shared message parsing and event dispatch helpers.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Reads an OSC argument by index and returns null when the argument is missing.
            /// </summary>
            /// <param name="message">OSC message to read.</param>
            /// <param name="index">Argument index to read.</param>
            /// <returns>The raw OSC argument, or <see langword="null"/> when unavailable.</returns>
            private static object? GetArgumentOrDefault(OscMessage message, int index)
            {
                try { return message[index]; }
                catch { return null; }
            }

            /// <summary>
            /// Converts common OSC primitive values to a float value.
            /// </summary>
            /// <param name="value">Raw OSC value to convert.</param>
            /// <returns>Converted float value, or 0 for unsupported values.</returns>
            private static float ConvertToSingle(object? value)
            {
                return value switch
                {
                    null => 0f,
                    float f => f,
                    double d => (float)d,
                    int i => i,
                    long l => l,
                    bool b => b ? 1f : 0f,
                    _ => 0f,
                };
            }

            /// <summary>
            /// Converts common OSC primitive values to a boolean value.
            /// </summary>
            /// <param name="value">Raw OSC value to convert.</param>
            /// <returns><see langword="true"/> for true booleans, nonzero integers, or numeric values with magnitude at least 0.5.</returns>
            private static bool ConvertToBool(object? value)
            {
                return value switch
                {
                    null => false,
                    bool b => b,
                    int i => i != 0,
                    long l => l != 0,
                    float f => Math.Abs(f) >= 0.5f,
                    double d => Math.Abs(d) >= 0.5,
                    _ => false,
                };
            }

            /// <summary>
            /// Reads up to a maximum number of numeric OSC arguments as floats.
            /// </summary>
            /// <param name="message">OSC message to read.</param>
            /// <param name="maxCount">Maximum number of arguments to read.</param>
            /// <returns>Float values read before the first missing argument.</returns>
            private static IReadOnlyList<float> ReadFloatArguments(OscMessage message, int maxCount)
            {
                var values = new List<float>(maxCount);
                for (var i = 0; i < maxCount; i++)
                {
                    object? raw;
                    try
                    {
                        raw = message[i];
                    }
                    catch
                    {
                        break;
                    }

                    values.Add(ConvertToSingle(raw));
                }

                return values;
            }

            /// <summary>
            /// Reads a float value from a list or returns 0 when the index is outside the list.
            /// </summary>
            /// <param name="values">Float values to inspect.</param>
            /// <param name="index">Index to read.</param>
            /// <returns>The value at the index, or 0.</returns>
            private static float GetRaw(IReadOnlyList<float> values, int index)
                => index >= 0 && index < values.Count ? values[index] : 0f;

            /// <summary>
            /// Invokes an OSC event handler while isolating subscriber exceptions.
            /// </summary>
            /// <typeparam name="T">Payload type supplied to the handler.</typeparam>
            /// <param name="handler">Event handler delegate to invoke.</param>
            /// <param name="payload">Payload to dispatch.</param>
            private static void InvokeSafely<T>(Action<T>? handler, T payload)
            {
                if (handler is null)
                    return;

                try { handler(payload); }
                catch (Exception ex)
                {
                    Console.WriteLine($"OSC event handler error ({typeof(T).Name}): {ex.Message}");
                }
            }
        }
    }
}
