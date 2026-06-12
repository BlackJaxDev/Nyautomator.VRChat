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
    /// Shared VRChat helper namespace for TCP command input.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that can receive plain-text command messages over TCP.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Gets whether the TCP command listener loop should continue accepting clients.
            /// </summary>
            public static bool ReceivingTCPCommands { get; private set; } = false;

            /// <summary>
            /// Starts a loopback TCP listener and parses each received text chunk as an OSC helper command.
            /// </summary>
            /// <param name="port">Loopback TCP port to bind.</param>
            /// <returns>A task that completes when the listener stops or an accept-loop exception occurs.</returns>
            public static async Task StartReceivingTCPCommands(int port = 8000)
            {
                ReceivingTCPCommands = true;
                var localAddress = IPAddress.Loopback;
                var listener = new TcpListener(localAddress, port);
                try
                {
                    listener.Start();
                    Console.WriteLine("Listening for TCP messages on port {0}", port);
                    while (ReceivingTCPCommands)
                    {
                        await HandleConnectionAsync(await listener.AcceptTcpClientAsync());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: {0}", e.Message);
                }
            }

            /// <summary>
            /// Requests that the TCP command listener stop accepting additional clients.
            /// </summary>
            public static void StopReceivingTCPCommands()
            {
                ReceivingTCPCommands = false;
            }

            /// <summary>
            /// Reads ASCII command messages from one TCP client and passes them to the command parser.
            /// </summary>
            /// <param name="client">Connected TCP client.</param>
            /// <returns>A task that completes when the client disconnects or fails.</returns>
            private static async Task HandleConnectionAsync(TcpClient client)
            {
                try
                {
                    using (client)
                    {
                        NetworkStream stream = client.GetStream();
                        byte[] buffer = new byte[1024];
                        int numberOfBytesRead;
                        while ((numberOfBytesRead = await stream.ReadAsync(buffer)) != 0)
                        {
                            string msg = Encoding.ASCII.GetString(buffer, 0, numberOfBytesRead);
                            Console.WriteLine("Received: {0}", msg);
                            ParseCommandMessage(msg);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error handling client: {0}", e.Message);
                }
            }
        }
    }
}
