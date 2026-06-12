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
    /// Shared VRChat helper namespace for OSC UDP transport.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that manages UDP sending, listening, and passthrough forwarding.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Gets or creates the UDP sender connected to the configured VRChat OSC sender port.
            /// </summary>
            private static UdpClient Sender
            {
                get
                {
                    if (sender is not null)
                        return sender;

                    sender = new UdpClient();
                    sender.Connect(IPAddress.Loopback, _senderPort);
                    return sender;
                }
            }

            /// <summary>
            /// Gets or creates the UDP receiver bound to the configured listener port.
            /// </summary>
            private static UdpClient Receiver
                => receiver ??= CreateUdpReceiver(_receiverPort);

            /// <summary>
            /// Creates a UDP receiver bound to any local interface with address reuse enabled.
            /// </summary>
            /// <param name="port">Local UDP port to bind.</param>
            /// <returns>A bound UDP client.</returns>
            private static UdpClient CreateUdpReceiver(int port)
            {
                var client = new UdpClient(AddressFamily.InterNetwork);
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                return client;
            }

            /// <summary>
            /// UDP port used when sending OSC messages to VRChat.
            /// </summary>
            private static int _senderPort = 9000;

            /// <summary>
            /// UDP port used when listening for OSC messages from VRChat.
            /// </summary>
            private static int _receiverPort = 9001;

            /// <summary>
            /// Lazily created UDP sender client.
            /// </summary>
            private static UdpClient? sender;

            /// <summary>
            /// Lazily created UDP receiver client.
            /// </summary>
            private static UdpClient? receiver;

            /// <summary>
            /// Synchronizes passthrough port and socket changes.
            /// </summary>
            private static readonly object PassthroughSync = new();

            /// <summary>
            /// External UDP input port used for OSC passthrough.
            /// </summary>
            private static int _passthroughInputPort = 9010;

            /// <summary>
            /// External UDP output port used for OSC passthrough broadcasts.
            /// </summary>
            private static int _passthroughOutputPort = 9011;

            /// <summary>
            /// UDP receiver used to accept external OSC passthrough input.
            /// </summary>
            private static UdpClient? passthroughInput;

            /// <summary>
            /// UDP sender used to broadcast observed OSC traffic to the external passthrough output port.
            /// </summary>
            private static UdpClient? passthroughOutput;

            /// <summary>
            /// Cancellation source for the passthrough input loop.
            /// </summary>
            private static CancellationTokenSource? passthroughCts;

            /// <summary>
            /// Background task that receives external passthrough input.
            /// </summary>
            private static Task? passthroughTask;

            /// <summary>
            /// Gets whether OSC passthrough is currently enabled.
            /// </summary>
            public static bool PassthroughEnabled { get; private set; }

            /// <summary>
            /// Gets the external input port currently configured for passthrough.
            /// </summary>
            public static int ExternalInputPort => _passthroughInputPort;

            /// <summary>
            /// Gets the external output port currently configured for passthrough.
            /// </summary>
            public static int ExternalOutputPort => _passthroughOutputPort;

            /// <summary>
            /// Gets or sets the VRChat OSC sender port, resetting the sender socket when changed.
            /// </summary>
            public static int SenderPort 
            {
                get => _senderPort;
                set
                {
                    _senderPort = value;
                    sender?.Close();
                    sender = null;
                }
            }

            /// <summary>
            /// Gets or sets the VRChat OSC listener port, resetting the receiver socket when changed.
            /// </summary>
            public static int ReceiverPort
            {
                get => _receiverPort;
                set
                {
                    _receiverPort = value;
                    receiver?.Close();
                    receiver = null;
                }
            }

            /// <summary>
            /// Enables, disables, or reconfigures OSC passthrough ports.
            /// </summary>
            /// <param name="enabled">Whether passthrough should be enabled.</param>
            /// <param name="inputPort">External UDP port to receive passthrough input from.</param>
            /// <param name="outputPort">External UDP port to broadcast observed OSC traffic to.</param>
            public static void ConfigurePassthrough(bool enabled, int inputPort, int outputPort)
            {
                lock (PassthroughSync)
                {
                    var normalizedInput = inputPort > 0 ? inputPort : 9010;
                    var normalizedOutput = outputPort > 0 ? outputPort : 9011;
                    bool portsChanged = _passthroughInputPort != normalizedInput || _passthroughOutputPort != normalizedOutput;

                    _passthroughInputPort = normalizedInput;
                    _passthroughOutputPort = normalizedOutput;

                    if (!enabled)
                    {
                        StopPassthroughLocked();
                        return;
                    }

                    if (PassthroughEnabled && !portsChanged)
                        return;

                    StopPassthroughLocked();
                    StartPassthroughLocked();
                }
            }

            /// <summary>
            /// Starts passthrough sockets and ensures the normal OSC listener is running.
            /// </summary>
            private static void StartPassthroughLocked()
            {
                try
                {
                    passthroughInput = CreateUdpReceiver(_passthroughInputPort);
                    passthroughOutput = new UdpClient();
                    passthroughOutput.Connect(IPAddress.Loopback, _passthroughOutputPort);

                    passthroughCts = new CancellationTokenSource();
                    passthroughTask = Task.Run(() => RunPassthroughInputLoop(passthroughCts.Token));
                    PassthroughEnabled = true;
                    Console.WriteLine($"OSC passthrough enabled (external in {_passthroughInputPort}, out {_passthroughOutputPort}).");

                    if (!Listening)
                    {
                        _ = StartListening(_receiverPort).ContinueWith(
                            static t => Console.WriteLine($"OSC listener faulted: {t.Exception?.GetBaseException().Message}"),
                            TaskContinuationOptions.OnlyOnFaulted);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to enable OSC passthrough: {ex.Message}");
                    StopPassthroughLocked();
                }
            }

            /// <summary>
            /// Stops passthrough sockets and cancels the passthrough input loop.
            /// </summary>
            private static void StopPassthroughLocked()
            {
                var wasEnabled = PassthroughEnabled;
                PassthroughEnabled = false;

                try { passthroughCts?.Cancel(); } catch { }
                passthroughCts?.Dispose();
                passthroughCts = null;

                try { passthroughInput?.Close(); } catch { }
                passthroughInput = null;

                try { passthroughOutput?.Close(); } catch { }
                passthroughOutput = null;

                passthroughTask = null;

                if (wasEnabled)
                    Console.WriteLine("OSC passthrough disabled.");
            }

            /// <summary>
            /// Gets whether the UDP listener loop is currently active.
            /// </summary>
            public static bool Listening { get; private set; }

            /// <summary>
            /// Gets whether OSC sending is currently enabled.
            /// </summary>
            public static bool Sending { get; private set; }

            /// <summary>
            /// Enables OSC sending to the supplied VRChat UDP port.
            /// </summary>
            /// <param name="port">VRChat OSC input port.</param>
            public static void StartSending(int port = 9000)
            {
                SenderPort = port;
                Sending = true;
                Console.WriteLine("Sending OSC messages...");
            }

            /// <summary>
            /// Disables OSC sending and closes the sender socket.
            /// </summary>
            public static void StopSending()
            {
                Sending = false;
                Console.WriteLine("Stopped sending OSC messages");
                sender?.Close();
                sender = null;
            }

            /// <summary>
            /// Stops the OSC listener loop and closes the receiver socket.
            /// </summary>
            public static void StopListening()
            {
                Console.WriteLine("Stopped listening for OSC messages");
                Listening = false;
                receiver?.Close();
                receiver = null;
            }

            /// <summary>
            /// Starts the UDP listener loop and dispatches received OSC packets until stopped.
            /// </summary>
            /// <param name="port">Local UDP port to listen on.</param>
            /// <returns>A task that completes when listening stops.</returns>
            public static async Task StartListening(int port = 9001)
            {
                ReceiverPort = port;
                Console.WriteLine("Listening for OSC messages...");
                Listening = true;
                while (Listening)
                {
                    try
                    {
                        var result = await Receiver.ReceiveAsync();
                        byte[] receivedBytes = result.Buffer;
                        OscPacket packet = OscPacket.Read(receivedBytes, 0, receivedBytes.Length);

                        if (packet is OscBundle bundle)
                        {
                            Console.WriteLine("Received an OSC Bundle");
                            var enumerator = bundle.Messages();
                            while (enumerator.MoveNext())
                            {
                                var element = enumerator.Current;
                                if (element is OscMessage message)
                                    ReceiveOscMessage(message);
                            }
                        }
                        else if (packet is OscMessage message)
                            ReceiveOscMessage(message);

                        ForwardOutgoingBuffer(receivedBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }

            /// <summary>
            /// Receives external passthrough OSC packets, dispatches them locally, and forwards them to VRChat.
            /// </summary>
            /// <param name="token">Token that cancels the passthrough input loop.</param>
            /// <returns>A task that completes when the loop exits.</returns>
            private static async Task RunPassthroughInputLoop(CancellationToken token)
            {
                var input = passthroughInput;
                if (input is null)
                    return;

                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result;
                    try
                    {
                        result = await input.ReceiveAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (!PassthroughEnabled)
                            break;

                        Console.WriteLine($"OSC passthrough receive error: {ex.Message}");
                        try { await Task.Delay(200).ConfigureAwait(false); } catch { }
                        continue;
                    }

                    if (token.IsCancellationRequested)
                        break;

                    var buffer = result.Buffer;
                    if (buffer is null || buffer.Length == 0)
                        continue;

                    try
                    {
                        var packet = OscPacket.Read(buffer, 0, buffer.Length);
                        DispatchIncomingPacket(packet);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OSC passthrough parse error: {ex.Message}");
                    }

                    try
                    {
                        Sender.Send(buffer, buffer.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OSC passthrough forward error: {ex.Message}");
                    }

                    ForwardOutgoingBuffer(buffer);
                }
            }

            /// <summary>
            /// Dispatches an incoming OSC packet, expanding bundles into individual messages.
            /// </summary>
            /// <param name="packet">OSC packet to dispatch.</param>
            private static void DispatchIncomingPacket(OscPacket packet)
            {
                if (packet is OscBundle bundle)
                {
                    var enumerator = bundle.Messages();
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current is OscMessage message)
                            ReceiveOscMessage(message);
                    }
                    return;
                }

                if (packet is OscMessage messageItem)
                    ReceiveOscMessage(messageItem);
            }

            /// <summary>
            /// Broadcasts an OSC packet buffer to the configured passthrough output port when enabled.
            /// </summary>
            /// <param name="buffer">Encoded OSC packet bytes to broadcast.</param>
            private static void ForwardOutgoingBuffer(byte[] buffer)
            {
                if (!PassthroughEnabled || buffer.Length == 0)
                    return;

                var output = passthroughOutput;
                if (output is null)
                    return;

                try
                {
                    output.Send(buffer, buffer.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OSC passthrough broadcast error: {ex.Message}");
                }
            }

            /// <summary>
            /// Sends one OSC message to VRChat and mirrors the encoded payload to passthrough output.
            /// </summary>
            /// <param name="message">OSC message to send.</param>
            private static void SendOscMessage(OscMessage message)
            {
                var payload = message.ToByteArray();
                try
                {
                    Sender.Send(payload, payload.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"OSC send error: {ex.Message}");
                    return;
                }

                ForwardOutgoingBuffer(payload);
            }

            /// <summary>
            /// Routes a received OSC message to avatar-change, camera, or avatar-parameter handlers.
            /// </summary>
            /// <param name="message">OSC message received from VRChat or passthrough input.</param>
            private static void ReceiveOscMessage(OscMessage message)
            {
                var path = message.Address;
                //Console.WriteLine($"Received OSC Message: {path}");
                if (path.StartsWith(AvatarChangePath))
                    HandleAvatarChange(message);
                else if (path.StartsWith(UserCameraPathPrefix, StringComparison.OrdinalIgnoreCase))
                    HandleCameraMessage(message);
                else if (path.StartsWith(AvatarParameterPathPrefix))
                    HandleAvatarParameters(message);
            }
        }
    }
}
