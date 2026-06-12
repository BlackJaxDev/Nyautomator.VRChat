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
    /// Shared VRChat helper namespace for WebSocket command input.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that can receive plain-text command messages over WebSockets.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Gets whether the outbound WebSocket command client should continue receiving broadcast commands.
            /// </summary>
            public static bool ReceivingWebsocketBroadcastCommands { get; private set; } = false;

            /// <summary>
            /// Connects to a local WebSocket server and parses received text messages as OSC helper commands.
            /// </summary>
            /// <param name="port">Local WebSocket port to connect to.</param>
            /// <returns>A task that completes when receiving stops or the socket closes.</returns>
            public static async Task StartWebsocketClient(int port = 8090)
            {
                ReceivingWebsocketBroadcastCommands = true;
                ClientWebSocket client = new();
                try
                {
                    await client.ConnectAsync(new Uri($"ws://localhost:{port}"), CancellationToken.None);
                    Console.WriteLine("Connected to server");
                    byte[] buffer = new byte[1024];
                    while (ReceivingWebsocketBroadcastCommands)
                    {
                        WebSocketReceiveResult result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine("Received: {0}", msg);
                        ParseCommandMessage(msg);
                    }
                }
                catch (Exception ex) when (ex is System.Net.Sockets.SocketException or WebSocketException or ObjectDisposedException or OperationCanceledException)
                {
                    Console.WriteLine($"OSC WebSocket client stopped: {ex.Message}");
                }
            }
            /// <summary>
            /// Requests that the local WebSocket client receive loop stop.
            /// </summary>
            public static void StopWebsocketClient()
            {
                ReceivingWebsocketBroadcastCommands = false;
            }
            /// <summary>
            /// Gets whether the local WebSocket command server is accepting requests.
            /// </summary>
            public static bool WebsocketServerRunning { get; private set; } = false;

            /// <summary>
            /// Starts an HTTP listener that accepts local WebSocket clients and parses received commands.
            /// </summary>
            /// <param name="port">Local HTTP/WebSocket port to bind.</param>
            /// <returns>A task that completes when the server loop stops or the listener fails.</returns>
            public static async Task StartWebsocketServer(int port = 8090)
            {
                HttpListener server = new();
                string endpoint = $"http://localhost:{port}/";
                server.Prefixes.Add(endpoint);
                server.Start();
                Console.WriteLine($"OSC websocket server started at {endpoint}");
                WebsocketServerRunning = true;
                while (WebsocketServerRunning)
                {
                    HttpListenerContext listenerContext = await server.GetContextAsync();
                    if (listenerContext.Request.IsWebSocketRequest)
                    {
                        try
                        {
                            HttpListenerWebSocketContext webSocketContext = await listenerContext.AcceptWebSocketAsync(subProtocol: null);
                            Console.WriteLine("OSC webserver client connected.");
                            await HandleConnection(webSocketContext.WebSocket);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                            listenerContext.Response.StatusCode = 400;
                            listenerContext.Response.Close();
                        }
                    }
                    else
                    {
                        listenerContext.Response.StatusCode = 400;
                        listenerContext.Response.Close();
                        Console.WriteLine("HTTP request without WebSocket");
                    }
                }
            }

            /// <summary>
            /// Reads UTF-8 text messages from a connected WebSocket and passes them to the command parser.
            /// </summary>
            /// <param name="socket">Accepted WebSocket connection.</param>
            /// <returns>A task that completes when the socket closes or fails.</returns>
            private static async Task HandleConnection(WebSocket socket)
            {
                byte[] buffer = new byte[1024];
                try
                {
                    while (socket.State == WebSocketState.Open)
                    {
                        WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine("Received: {0}", msg);
                        ParseCommandMessage(msg);
                    }
                }
                catch (Exception ex) when (ex is System.Net.Sockets.SocketException or WebSocketException or ObjectDisposedException or OperationCanceledException)
                {
                    // Connection closed or aborted — expected during shutdown
                }
                Console.WriteLine("OSC webserver client disconnected.");
            }
        }
    }
}
