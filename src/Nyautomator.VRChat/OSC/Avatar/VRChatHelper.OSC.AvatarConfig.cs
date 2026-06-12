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
    /// Shared VRChat helper namespace for avatar OSC configuration loading.
    /// </summary>
    public static partial class VRChatHelper
    {
        /// <summary>
        /// Static OSC helper that tracks the current avatar configuration file used for parameters.
        /// </summary>
        public partial class OSC
        {
            /// <summary>
            /// Loads the newest avatar config found under likely VRChat OSC roots and rebuilds parameter caches.
            /// </summary>
            /// <returns><see langword="true"/> when a valid avatar config was found and applied.</returns>
            public static bool TryLoadLatestAvatarConfig()
            {
                try
                {
                    string? latestFile = null;
                    DateTime latestWrite = DateTime.MinValue;

                    foreach (var root in CandidateOSCRoots())
                    {
                        if (!Directory.Exists(root)) continue;
                        foreach (var userDir in Directory.GetDirectories(root))
                        {
                            string avatarDir = Path.Combine(userDir, "Avatars");
                            if (!Directory.Exists(avatarDir)) continue;
                            foreach (var file in Directory.GetFiles(avatarDir, "*", SearchOption.TopDirectoryOnly))
                            {
                                var write = File.GetLastWriteTimeUtc(file);
                                if (write > latestWrite)
                                {
                                    latestWrite = write;
                                    latestFile = file;
                                }
                            }
                        }
                    }

                    if (latestFile == null)
                        return false;

                    var configText = File.ReadAllText(latestFile);
                    var config = JsonConvert.DeserializeObject<AvatarConfig>(configText);
                    if (config?.Id is null)
                        return false;

                    Config = config;
                    RemakeDictionaries();
                    try { OnAvatarConfigChanged?.Invoke(); } catch { }
                    return true;
                }
                catch { return false; }
            }

            /// <summary>
            /// Builds a list of OSC root folders from an environment override, the current profile, and other user profiles.
            /// </summary>
            /// <returns>Existing directories that may contain VRChat OSC avatar configs.</returns>
            private static List<string> CandidateOSCRoots()
            {
                var results = new List<string>();

                try
                {
                    var env = Environment.GetEnvironmentVariable("VRCHAT_OSC_DIR");
                    if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
                    {
                        var last = Path.GetFileName(env.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        if (string.Equals(last, "OSC", StringComparison.OrdinalIgnoreCase))
                            results.Add(env);
                        else
                        {
                            var parent = Path.GetDirectoryName(env);
                            if (!string.IsNullOrEmpty(parent))
                            {
                                var parentName = Path.GetFileName(parent);
                                if (string.Equals(parentName, "OSC", StringComparison.OrdinalIgnoreCase))
                                    results.Add(parent);
                                else
                                    results.Add(env);
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    if (Directory.Exists(VRChatHelper.OSCDir))
                        results.Add(VRChatHelper.OSCDir);
                }
                catch { }

                try
                {
                    var usersRoot = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "Users");
                    if (Directory.Exists(usersRoot))
                    {
                        foreach (var u in Directory.EnumerateDirectories(usersRoot))
                        {
                            var name = Path.GetFileName(u)?.ToLowerInvariant();
                            if (name is "public" or "default" or "default user" or "all users") continue;

                            var osc = Path.Combine(u, "AppData", "LocalLow", "VRChat", "VRChat", "OSC");
                            if (Directory.Exists(osc))
                                results.Add(osc);
                        }
                    }
                }
                catch { }

                return results;
            }

            /// <summary>
            /// Locates the config file for a newly reported avatar ID and replaces the active avatar config.
            /// </summary>
            /// <param name="newId">Avatar ID reported by VRChat's /avatar/change message.</param>
            public static void ParseNewAvatar(string newId)
            {
                foreach (var root in CandidateOSCRoots())
                {
                    foreach (var userDir in Directory.GetDirectories(root))
                    {
                        string avatarDir = Path.Combine(userDir, "Avatars");
                        if (!Directory.Exists(avatarDir))
                            continue;
                        
                        foreach (var avatarConfigPath in Directory.GetFiles(avatarDir))
                        {
                            var configText = File.ReadAllText(avatarConfigPath);
                            var config = JsonConvert.DeserializeObject<AvatarConfig>(configText);
                            if (config?.Id is not null && string.Equals(config.Id, newId, StringComparison.InvariantCulture))
                            {
                                Config = config;
                                RemakeDictionaries();
                                try { OnAvatarConfigChanged?.Invoke(); } catch { /* ignore */ }
                                return;
                            }
                        }
                    }
                }

                if (Config is null)
                {
                    Trace.WriteLine($"Avatar config file for {newId} not found in any OSC directory");
                }
            }

            /// <summary>
            /// Rebuilds the typed parameter caches from built-in parameter definitions and the active avatar config.
            /// </summary>
            private static void RemakeDictionaries()
            {
                FloatParams.Clear();
                IntParams.Clear();
                BoolParams.Clear();
                foreach (var parameter in BuiltInParameterNames)
                {
                    switch (parameter.Value.Type)
                    {
                        case Int:
                            IntParams.TryAdd(parameter.Key, 0);
                            break;
                        case Bool:
                            BoolParams.TryAdd(parameter.Key, false);
                            break;
                        case Float:
                            FloatParams.TryAdd(parameter.Key, 0.0f);
                            break;
                    }
                }
                if (Config?.Parameters is not null)
                {
                    foreach (var parameter in Config.Parameters)
                    {
                        if (parameter.Name is null)
                            continue;

                        if (parameter.Input is not null && parameter.Input.Type is not null)
                        {
                            switch (parameter.Input.Type)
                            {
                                case Int:
                                    IntParams.TryAdd(parameter.Name, 0);
                                    break;
                                case Bool:
                                    BoolParams.TryAdd(parameter.Name, false);
                                    break;
                                case Float:
                                    FloatParams.TryAdd(parameter.Name, 0.0f);
                                    break;
                            }
                        }
                        if (parameter.Output is not null && parameter.Output.Type is not null)
                        {
                            switch (parameter.Output.Type)
                            {
                                case Int:
                                    IntParams.TryAdd(parameter.Name, 0);
                                    break;
                                case Bool:
                                    BoolParams.TryAdd(parameter.Name, false);
                                    break;
                                case Float:
                                    FloatParams.TryAdd(parameter.Name, 0.0f);
                                    break;
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Handles VRChat's avatar change OSC message and loads the matching avatar config.
            /// </summary>
            /// <param name="message">OSC message whose first argument is the new avatar ID.</param>
            private static void HandleAvatarChange(OscMessage message)
            {
                string avatarId = (string)message[0];
                ParseNewAvatar(avatarId);
                Console.WriteLine($"Avatar changed: {avatarId}");
            }
        }
    }
}
