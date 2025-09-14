using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Enums;
using KitchenLib.Logging;

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

            try
            {
                var jsonAsm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Newtonsoft.Json");
                if (jsonAsm != null)
                {
                    Logger.LogInfo($"[Json] Using Newtonsoft.Json v{jsonAsm.GetName().Version} ({SafeLocation(jsonAsm)})");
                }
                else
                {
                    Logger.LogInfo("[Json] Newtonsoft.Json not yet loaded at connect time.");
                }
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

            try
            {
                var apAsms = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name == "Archipelago.MultiClient.Net")
                    .ToList();
                foreach (var a in apAsms)
                    Logger.LogInfo($"[APDiag] Loaded Archipelago assembly: {a.FullName} @ {SafeLocation(a)}");
                if (apAsms.Count > 1)
                    Logger.LogError("[APDiag] Multiple Archipelago.MultiClient.Net assemblies loaded – this will break enum casting.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[APDiag] Assembly scan failed: " + ex.Message);
            }

            var candidateUris = BuildCandidateUris(ip, port);

            foreach (var uri in candidateUris)
            {
                if (_sawPermissionsCastError)
                {
                    Logger.LogError("Aborting further attempts due to detected Permissions enum cast mismatch.");
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
                        password: string.IsNullOrWhiteSpace(password) ? null : password
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
                foreach (ConnectionRefusedError error in failure.ErrorCodes) errorMessage += "\n    " + error;
                Logger.LogError(errorMessage);

                if (_sawPermissionsCastError)
                {
                    Logger.LogError("Detected Permissions serialization cast error. If issues persist, update Newtonsoft.Json or keep the compatibility patch.");
                }

                CleanupSocketEvents();
            }

            IsConnecting = false;
            if (!ConnectionSuccessful)
            {
                if (_sawPermissionsCastError)
                    Logger.LogError("Connection aborted due to Permissions enum cast mismatch.");
                else
                    Logger.LogError("All connection attempts failed.");
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
                    try
                    {
                        var ub = new UriBuilder(provided) { Port = port };
                        provided = ub.Uri;
                    }
                    catch { }
                }
                list.Add(provided);
                return list;
            }

            try { list.Add(new UriBuilder("wss", hostInput, port).Uri); }
            catch (Exception ex) { Logger.LogWarning("Failed to build wss URI: " + ex.Message); }

            try { list.Add(new UriBuilder("ws", hostInput, port).Uri); }
            catch (Exception ex) { Logger.LogWarning("Failed to build ws URI: " + ex.Message); }

            list = list.Distinct().ToList();
            return list;
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
            if (e.StackTrace != null)
            {
                foreach (var line in e.StackTrace.Split('\n'))
                    Logger.LogInfo("    " + line);
            }
            else
            {
                Logger.LogInfo("    No stacktrace provided");
            }

            if (!_sawPermissionsCastError &&
                (message.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 e.Message.IndexOf("cast", StringComparison.OrdinalIgnoreCase) >= 0) &&
                e.StackTrace?.IndexOf("PermissionsEnumConverter", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _sawPermissionsCastError = true;
                Logger.LogError("Permissions enum cast error detected (handled by compatibility patch).");
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
