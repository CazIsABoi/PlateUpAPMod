using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using KitchenLib.Logging;

namespace KitchenPlateupAP
{
    public static class ArchipelagoConnectionManager
    {
        private static readonly KitchenLogger Logger = new KitchenLogger("ArchipelagoConnectionManager");

        private static readonly object _stateLock = new object();

        private static ArchipelagoSession _session;

        private static string _lastHost;
        private static int _lastPort;
        private static string _lastPlayerName;
        private static string _lastPassword;
        private static ItemsHandlingFlags _lastFlags;
        private static CancellationTokenSource _reconnectCts;
        private static bool _suppressReconnect;
        private const int ReconnectDelaySeconds = 5;
        private const int MaxReconnectAttempts = 10;
        private const int MaxReconnectDelaySeconds = 60;

        public static ArchipelagoSession Session
        {
            get { lock (_stateLock) return _session; }
            private set { lock (_stateLock) _session = value; }
        }

        public static bool IsConnecting { get; private set; }
        public static bool IsConnected { get; private set; }


        public static bool ConnectionSuccessful => IsConnected;

        public static Dictionary<string, object> SlotData { get; private set; }
        public static int SlotIndex { get; private set; }

        public static event Action Connected;
        public static event Action<string> ConnectionFailed;
        public static event Action<string> Disconnected;

        private const string GameName = "plateup";

        static ArchipelagoConnectionManager()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }

        [Obsolete("Use ConnectAsync(...) instead. This fires-and-forgets and may swallow errors.")]
        public static void TryConnect(string host, int port, string playerName, string password)
        {
            _ = ConnectAsync(host, port, playerName, password, ItemsHandlingFlags.AllItems, requestSlotData: true);
        }

        /// <summary>
        /// Ensures JsonConvert.DefaultSettings won't interfere with Archipelago's
        /// internal JSON serialization (e.g. ItemsHandlingFlags in the ConnectPacket).
        /// </summary>
        private static void SanitizeJsonDefaults()
        {
            try
            {
                Newtonsoft.Json.JsonConvert.DefaultSettings = null;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Failed to clear JsonConvert.DefaultSettings: " + ex.Message);
            }
        }

        public static async Task<bool> ConnectAsync(
            string host,
            int port,
            string playerName,
            string password = "",
            ItemsHandlingFlags flags = ItemsHandlingFlags.AllItems,
            bool requestSlotData = true)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                Fail("Host is required.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(playerName))
            {
                Fail("Player (slot) name is required.");
                return false;
            }

            lock (_stateLock)
            {
                if (IsConnecting)
                {
                    Logger.LogInfo("A connection attempt is already in progress.");
                    return false;
                }
                if (IsConnected)
                {
                    Logger.LogWarning("Already connected. Disconnect first before connecting again.");
                    Fail("Already connected to Archipelago. Disconnect first.");
                    return false;
                }
                IsConnecting = true;
            }

            try
            {
                await DisconnectAsync(suppressReconnect: true).ConfigureAwait(false);

                _lastHost = host;
                _lastPort = port;
                _lastPlayerName = playerName;
                _lastPassword = password;
                _lastFlags = flags;

                // Clear any custom JSON serializer settings that other mods (e.g. KitchenLib)
                // may have installed globally. These can corrupt the ConnectPacket's
                // ItemsHandlingFlags serialization, causing "invalid item handling flags".
                SanitizeJsonDefaults();

                var session = ArchipelagoSessionFactory.CreateSession(host, port);
                WireSession(session);
                Session = session;

                Logger.LogInfo($"Connecting to {host}:{port} as '{playerName}'...");
                await session.ConnectAsync().ConfigureAwait(false);

                Logger.LogInfo("Logging in...");
                var result = await session.LoginAsync(
                    GameName,
                    playerName,
                    flags,
                    password: password,
                    requestSlotData: requestSlotData
                ).ConfigureAwait(false);

                if (result.Successful)
                {
                    var success = (LoginSuccessful)result;

                    SlotIndex = success.Slot;
                    SlotData = success.SlotData;
                    IsConnected = true;

                    Logger.LogInfo($"Connected. Slot {SlotIndex}, Player '{playerName}'.");
                    SafeInvokeConnected();

                    try { Mod.Instance?.OnSuccessfulConnect(); } catch (Exception ex) { Logger.LogError("OnSuccessfulConnect callback error: " + ex.Message); }

                    return true;
                }
                else
                {
                    var failure = (LoginFailure)result;
                    var errors = (failure.Errors ?? Array.Empty<string>()).ToArray();
                    var errorText = errors.Length == 0 ? "Unknown login failure." : string.Join("; ", errors);

                    Logger.LogError("Login failed: " + errorText);
                    Fail(errorText);
                    await DisconnectAsync().ConfigureAwait(false);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Connection error: " + ex.GetBaseException().Message);
                Fail(ex.GetBaseException().Message);
                await DisconnectAsync().ConfigureAwait(false);
                return false;
            }
            finally
            {
                lock (_stateLock) IsConnecting = false;
            }
        }

        public static async Task<bool> ReconnectAsync()
        {
            // Reconnect must bypass the "already connected" guard since it
            // is called after a disconnect event. Ensure cleanup first.
            await DisconnectAsync(suppressReconnect: true).ConfigureAwait(false);
            return await ConnectAsync(_lastHost, _lastPort, _lastPlayerName, _lastPassword, _lastFlags).ConfigureAwait(false);
        }

        public static async Task DisconnectAsync(string reason = null, bool suppressReconnect = false)
        {
            _suppressReconnect = suppressReconnect;
            _reconnectCts?.Cancel();

            var s = Session;
            if (s == null) return;

            try
            {
                UnwireSession(s);
            }
            catch { }

            try
            {
                await s.Socket.DisconnectAsync().ConfigureAwait(false);
            }
            catch { }

            Session = null;

            if (IsConnected)
            {
                IsConnected = false;
                SafeInvokeDisconnected(reason ?? "Client disconnected");
            }
        }


        private static void WireSession(ArchipelagoSession session)
        {
            if (session == null) return;
            session.Socket.SocketOpened += OnSocketOpened;
            session.Socket.PacketReceived += OnPacketReceived;
            session.Socket.SocketClosed += OnSocketClosed;
            session.Socket.ErrorReceived += OnErrorReceived;
        }

        private static void UnwireSession(ArchipelagoSession session)
        {
            try
            {
                if (session == null) return;
                session.Socket.SocketOpened -= OnSocketOpened;
                session.Socket.PacketReceived -= OnPacketReceived;
                session.Socket.SocketClosed -= OnSocketClosed;
                session.Socket.ErrorReceived -= OnErrorReceived;
            }
            catch { }
        }

        private static void OnSocketOpened()
        {
            Logger.LogInfo("Socket opened.");
        }

        private static void OnSocketClosed(string reason)
        {
            Logger.LogInfo("Socket closed: " + reason);
            if (IsConnected)
            {
                IsConnected = false;
                SafeInvokeDisconnected(reason);
            }
            ScheduleReconnect(reason);
        }

        private static void OnErrorReceived(Exception ex, string message)
        {
            Logger.LogError("Socket error: " + message);
            if (ex != null) Logger.LogError(ex.GetBaseException().ToString());
        }

        private static bool IsNonRetryableReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return false;

            return reason.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("user request", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ScheduleReconnect(string reason)
        {
            if (_suppressReconnect)
            {
                _suppressReconnect = false;
                return;
            }

            if (IsConnecting)
                return;

            // Don't auto-reconnect on obvious auth/user failures
            if (IsNonRetryableReason(reason))
            {
                Logger.LogWarning("Not auto-reconnecting due to failure reason: " + reason);
                try { ChatManager.AddSystemMessage("[Archipelago] Connection lost: " + reason); } catch { }
                return;
            }

            if (string.IsNullOrWhiteSpace(_lastHost) || string.IsNullOrWhiteSpace(_lastPlayerName))
            {
                Logger.LogWarning("Cannot auto-reconnect: missing last connection info.");
                return;
            }

            _reconnectCts?.Cancel();
            _reconnectCts = new CancellationTokenSource();
            var token = _reconnectCts.Token;

            Task.Run(async () =>
            {
                var rng = new System.Random();
                int attempt = 0;

                while (attempt < MaxReconnectAttempts && !token.IsCancellationRequested)
                {
                    attempt++;

                    // Exponential backoff: 5-7s, 10-14s, 20-28s... capped at 60s
                    int baseDelay = Math.Min(ReconnectDelaySeconds * (1 << (attempt - 1)), MaxReconnectDelaySeconds);
                    int jitter = rng.Next(0, Math.Max(1, baseDelay / 2));
                    int delaySeconds = baseDelay + jitter;

                    Logger.LogWarning($"Connection lost ({reason ?? "unknown"}). Reconnect attempt {attempt}/{MaxReconnectAttempts} in {delaySeconds}s...");
                    try { ChatManager.AddSystemMessage($"[Archipelago] Reconnecting ({attempt}/{MaxReconnectAttempts}) in {delaySeconds}s..."); } catch { }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
                    }
                    catch (TaskCanceledException)
                    {
                        return;
                    }

                    if (token.IsCancellationRequested)
                        return;

                    try
                    {
                        bool success = await ReconnectAsync().ConfigureAwait(false);
                        if (success)
                        {
                            Logger.LogInfo($"Auto-reconnect succeeded on attempt {attempt}.");
                            try { ChatManager.AddSystemMessage("[Archipelago] Reconnected successfully!"); } catch { }
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Auto-reconnect attempt {attempt} failed: " + ex.GetBaseException().Message);

                        // If the failure is non-retryable (e.g. server rejected credentials), stop trying
                        if (IsNonRetryableReason(ex.GetBaseException().Message))
                        {
                            Logger.LogWarning("Stopping reconnect: non-retryable error.");
                            try { ChatManager.AddSystemMessage("[Archipelago] Reconnect failed (non-retryable). Use Connect button."); } catch { }
                            return;
                        }
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    Logger.LogError($"Auto-reconnect failed after {MaxReconnectAttempts} attempts. Use the Connect button to reconnect manually.");
                    try { ChatManager.AddSystemMessage($"[Archipelago] Reconnect failed after {MaxReconnectAttempts} attempts. Use Connect button."); } catch { }
                }
            }, token);
        }

        private static void OnPacketReceived(ArchipelagoPacketBase packet)
        {
            if (packet == null) return;
            try
            {
                Logger.LogInfo("Packet: " + packet.PacketType);
                if (packet is ReceivedItemsPacket rip && rip.Items != null)
                {
                    Logger.LogInfo($"ReceivedItems: {rip.Items.Length} item(s).");
                }
            }
            catch { }
        }

        private static void OnItemReceived(ReceivedItemsHelper helper)
        {
            try
            {

            }
            catch (Exception ex)
            {
                Logger.LogError("OnItemReceived error: " + ex.Message);
            }
        }

        private static void SafeInvokeConnected()
        {
            try { Connected?.Invoke(); } catch (Exception e) { Logger.LogError("Connected event handler error: " + e.Message); }
        }

        private static void Fail(string msg)
        {
            try { ConnectionFailed?.Invoke(msg); } catch (Exception e) { Logger.LogError("ConnectionFailed event handler error: " + e.Message); }
        }

        private static void SafeInvokeDisconnected(string reason)
        {
            try { Disconnected?.Invoke(reason); } catch (Exception e) { Logger.LogError("Disconnected event handler error: " + e.Message); }
        }
    }
}