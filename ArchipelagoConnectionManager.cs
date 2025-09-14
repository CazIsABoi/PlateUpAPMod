using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Enums;
using KitchenLib.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KitchenPlateupAP
{
    public static class ArchipelagoConnectionManager
    {
        private static readonly KitchenLogger Logger = new KitchenLogger("ArchipelagoConnectionManager");
        public static ArchipelagoSession Session { get; private set; }
        public static bool ConnectionSuccessful { get; private set; }
        public static bool IsConnecting { get; private set; }
        public static Dictionary<string, object> SlotData;
        public static int SlotIndex;

        private static bool _sawPermissionsCastError;

        public static void TryConnect(string ip, int port, string playerName, string password)
        {
            if (IsConnecting)
                return;

            IsConnecting = true;
            ConnectionSuccessful = false;
            _sawPermissionsCastError = false;

            try { EnumIdentityDiagnostics(); }
            catch (Exception ex) { Logger.LogWarning("[APDiag] Enum diagnostics failed: " + ex.Message); }

            try
            {
                var jsonAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Newtonsoft.Json");
                if (jsonAsm != null)
                    Logger.LogInfo($"[Json] Using Newtonsoft.Json v{jsonAsm.GetName().Version} ({SafeLocation(jsonAsm)})");
                else
                    Logger.LogInfo("[Json] Newtonsoft.Json not yet loaded at connect time.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Json] Version diagnostic failed: " + ex.Message);
            }

            try
            {
                var archipelagoAsm = typeof(ArchipelagoSession).Assembly;
                Logger.LogInfo($"Archipelago Client Assembly: {archipelagoAsm.GetName().Name} v{archipelagoAsm.GetName().Version}");
            }
            catch (Exception ex)
            {
                Logger.LogInfo("Diagnostic metadata gathering failed: " + ex.Message);
            }

            var candidateUris = BuildCandidateUris(ip, port);

            foreach (var uri in candidateUris)
            {
                if (_sawPermissionsCastError)
                {
                    Logger.LogError("Aborting further attempts due to previous Permissions cast mismatch.");
                    break;
                }

                Logger.LogInfo("Attempting connection: " + uri);

                try
                {
                    Session = ArchipelagoSessionFactory.CreateSession(uri);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error creating session: " + ex.GetBaseException().Message);
                    continue;
                }

                TryInstallLenientStringListConverter();

                Session.Socket.ErrorReceived += OnSocketError;
                Session.Socket.SocketOpened += OnSocketOpened;
                Session.Socket.SocketClosed += OnSocketClosed;

                LoginResult result;
                try
                {
                    result = TryConnectAndLoginCompat(
                        game: "plateup",
                        name: playerName,
                        flags: ItemsHandlingFlags.AllItems,
                        password: password ?? string.Empty
                    );
                }
                catch (Exception e)
                {
                    Logger.LogError("Exception during TryConnectAndLogin: " + e.GetBaseException().Message);
                    result = new LoginFailure(e.GetBaseException().Message);
                }

                if (result.Successful)
                {
                    var loginSuccess = (LoginSuccessful)result;
                    ConnectionSuccessful = true;
                    SlotIndex = loginSuccess.Slot;
                    SlotData = loginSuccess.SlotData;
                    Logger.LogInfo($"Successfully connected to {uri} as slot '{playerName}'. Slot Index={SlotIndex}");
                    IsConnecting = false;
                    try { Mod.Instance.OnSuccessfulConnect(); }
                    catch (Exception ex) { Logger.LogError("OnSuccessfulConnect threw: " + ex.GetBaseException().Message); }
                    return;
                }

                var failure = (LoginFailure)result;
                var errorMessage = $"Failed to connect to {uri} as {playerName}:";
                foreach (string error in failure.Errors) errorMessage += "\n    " + error;
                foreach (ConnectionRefusedError code in failure.ErrorCodes) errorMessage += "\n    " + code;
                Logger.LogError(errorMessage);

                if (_sawPermissionsCastError)
                    Logger.LogError("Detected Permissions serialization cast error. Verify single Archipelago assembly and no custom JSON patches remain.");

                CleanupSocketEvents();
            }

            IsConnecting = false;
            if (!ConnectionSuccessful)
            {
                Logger.LogError(_sawPermissionsCastError
                    ? "Connection aborted due to Permissions enum cast mismatch."
                    : "All connection attempts failed.");
            }
        }

        // Adds a high-priority converter that ensures any List<string> (or string[]) gets only string items.
        private static void TryInstallLenientStringListConverter()
        {
            try
            {
                if (Session?.Socket == null) return;

                var socket = Session.Socket;
                var helperField = socket.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.FieldType.FullName != null && f.FieldType.FullName.Contains("BaseArchipelagoSocketHelper"));
                if (helperField == null) return;

                var helper = helperField.GetValue(socket);
                if (helper == null) return;

                var settingsField = helper.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(f => f.FieldType == typeof(JsonSerializerSettings));
                if (settingsField == null) return;

                var settings = settingsField.GetValue(helper) as JsonSerializerSettings;
                if (settings == null) return;

                if (!settings.Converters.Any(c => c.GetType() == typeof(LenientStringListConverter)))
                {
                    settings.Converters.Insert(0, new LenientStringListConverter());
                    Logger.LogInfo("[LenientStrings] Installed List<string> safety converter.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[LenientStrings] Install failed: " + ex.Message);
            }
        }

        // Converter that ensures list-of-strings targets never receive enum instances.
        private sealed class LenientStringListConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                if (objectType == typeof(List<string>) || objectType == typeof(string[]))
                    return true;

                if (objectType.IsGenericType &&
                    objectType.GetGenericTypeDefinition() == typeof(List<>) &&
                    objectType.GetGenericArguments()[0] == typeof(string))
                    return true;

                return false;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null)
                {
                    if (objectType == typeof(string[]))
                        return new string[0];
                    return new List<string>();
                }

                JArray arr = JArray.Load(reader);
                var list = new List<string>(arr.Count);
                foreach (var token in arr)
                {
                    if (token.Type == JTokenType.String)
                        list.Add(token.Value<string>());
                    else if (token.Type == JTokenType.Null)
                        list.Add(null);
                    else
                        list.Add(token.ToString());
                }

                if (objectType == typeof(string[]))
                    return list.ToArray();
                return list;
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();
                    return;
                }

                // Avoid conditional expressions mixing array/list (C# 8 limitation)
                IEnumerable<string> seq = null;
                var asArray = value as string[];
                if (asArray != null)
                {
                    seq = asArray;
                }
                else
                {
                    var asList = value as List<string>;
                    if (asList != null)
                        seq = asList;
                    else
                        seq = value as IEnumerable<string>;
                }

                writer.WriteStartArray();
                if (seq != null)
                {
                    foreach (var s in seq)
                        writer.WriteValue(s);
                }
                writer.WriteEndArray();
            }
        }

        private static void EnumIdentityDiagnostics()
        {
            string[] targets =
            {
                "Archipelago.MultiClient.Net.Enums.Permissions",
                "Archipelago.MultiClient.Net.Enums.ItemsHandlingFlags"
            };

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var name in targets)
            {
                var hits = new List<Type>();
                foreach (var asm in assemblies)
                {
                    try
                    {
                        var t = asm.GetType(name, false, false);
                        if (t != null) hits.Add(t);
                    }
                    catch { }
                }

                Logger.LogInfo($"[APDiag] {name} definitions found: {hits.Count}");
                int i = 0;
                foreach (var t in hits)
                {
                    var asm = t.Assembly;
                    string loc = SafeLocation(asm);
                    string underlying = Enum.GetUnderlyingType(t).Name;
                    string members = string.Join(",", Enum.GetNames(t));
                    Logger.LogInfo($"[APDiag]  [{i}] Asm={asm.GetName().Name} v{asm.GetName().Version} Path={loc} Hash={RuntimeHelpers.GetHashCode(asm)}");
                    Logger.LogInfo($"[APDiag]       AQN={t.AssemblyQualifiedName}");
                    Logger.LogInfo($"[APDiag]       Underlying={underlying} Members={members}");
                    i++;
                }
                if (hits.Count > 1)
                    Logger.LogError($"[APDiag] MULTIPLE {name} TYPE IDENTITIES DETECTED.");
            }
        }

        private static List<Uri> BuildCandidateUris(string hostInput, int port)
        {
            var list = new List<Uri>();
            if (Uri.TryCreate(hostInput, UriKind.Absolute, out var provided) &&
                (provided.Scheme == "ws" || provided.Scheme == "wss"))
            {
                if (provided.IsDefaultPort && port > 0)
                {
                    try { provided = new UriBuilder(provided) { Port = port }.Uri; } catch { }
                }
                list.Add(provided);
                return list;
            }
            try { list.Add(new UriBuilder("wss", hostInput, port).Uri); } catch { }
            try { list.Add(new UriBuilder("ws", hostInput, port).Uri); } catch { }
            return list.Distinct().ToList();
        }

        private static string SafeLocation(Assembly asm)
        {
            try { return string.IsNullOrEmpty(asm.Location) ? "<no path>" : asm.Location; }
            catch { return "<unavailable>"; }
        }

        private static LoginResult TryConnectAndLoginCompat(string game, string name, ItemsHandlingFlags flags, string password)
        {
            var sessionType = Session.GetType();
            var methods = sessionType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "TryConnectAndLogin").ToList();

            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length >= 3 &&
                    ps[0].ParameterType == typeof(string) &&
                    ps[1].ParameterType == typeof(string) &&
                    ps[2].ParameterType == typeof(ItemsHandlingFlags))
                {
                    object[] args = BuildArgs(ps, game, name, flags, password);
                    return (LoginResult)InvokeLogin(m, args);
                }
            }
            foreach (var m in methods)
            {
                var ps = m.GetParameters();
                if (ps.Length >= 2 &&
                    ps[0].ParameterType == typeof(string) &&
                    ps[1].ParameterType == typeof(string) &&
                    !ps.Any(p => p.ParameterType == typeof(ItemsHandlingFlags)))
                {
                    object[] args = BuildArgs(ps, game, name, null, password);
                    Logger.LogError("ItemsHandlingFlags overload not found – using legacy login.");
                    return (LoginResult)InvokeLogin(m, args);
                }
            }
            throw new MissingMethodException("No compatible TryConnectAndLogin overload found.");
        }

        private static object InvokeLogin(MethodInfo m, object[] args)
        {
            try
            {
                var result = m.Invoke(Session, args);
                Logger.LogInfo($"Login method chosen: {m}");
                return result;
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException ?? tie;
            }
        }

        private static object[] BuildArgs(ParameterInfo[] parameters, string game, string name, object flagsOrNull, string password)
        {
            var args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (i == 0) args[i] = game;
                else if (i == 1) args[i] = name;
                else if (flagsOrNull != null && p.ParameterType == typeof(ItemsHandlingFlags)) args[i] = flagsOrNull;
                else if (p.ParameterType == typeof(Version)) args[i] = null;
                else if (p.Name.Equals("password", StringComparison.OrdinalIgnoreCase)) args[i] = password;
                else if (p.Name.Equals("uuid", StringComparison.OrdinalIgnoreCase)) args[i] = null;
                else args[i] = p.HasDefaultValue ? p.DefaultValue : null;
            }
            return args;
        }

        private static void CleanupSocketEvents()
        {
            if (Session?.Socket == null) return;
            Session.Socket.ErrorReceived -= OnSocketError;
            Session.Socket.SocketOpened -= OnSocketOpened;
            Session.Socket.SocketClosed -= OnSocketClosed;
        }

        private static void OnSocketError(Exception e, string message)
        {
            Logger.LogInfo("Socket Error: " + message);
            Logger.LogInfo("Socket Exception: " + e.Message);

            if (!_sawPermissionsCastError &&
                (message.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 e.Message.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0) &&
                (e.StackTrace != null && e.StackTrace.IndexOf("PermissionsEnumConverter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 message.IndexOf("PermissionsEnumConverter", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                _sawPermissionsCastError = true;
                Logger.LogError("Permissions enum cast error detected.");
                try { EnumIdentityDiagnostics(); } catch { }
            }
        }

        private static void OnSocketOpened()
        {
            Logger.LogInfo("Socket opened to: " + Session.Socket.Uri);
        }

        private static void OnSocketClosed(string reason)
        {
            Logger.LogInfo("Socket closed: " + reason);
        }
    }
}
