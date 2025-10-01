using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;             
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
                IsConnecting = true;
            }

            try
            {
                await DisconnectAsync().ConfigureAwait(false);

                _lastHost = host;
                _lastPort = port;
                _lastPlayerName = playerName;
                _lastPassword = password;
                _lastFlags = flags;

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

                    try { session.Items.ItemReceived += OnItemReceived; } catch {}

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
            return await ConnectAsync(_lastHost, _lastPort, _lastPlayerName, _lastPassword, _lastFlags).ConfigureAwait(false);
        }

        public static async Task DisconnectAsync(string reason = null)
        {
            var s = Session;
            if (s == null) return;

            try
            {
                UnwireSession(s);
                try { s.Items.ItemReceived -= OnItemReceived; } catch { }
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
            session.Socket.SocketOpened += OnSocketOpened;
            session.Socket.PacketReceived += OnPacketReceived;
            session.Socket.SocketClosed += OnSocketClosed;
            session.Socket.ErrorReceived += OnErrorReceived;
        }

        private static void UnwireSession(ArchipelagoSession session)
        {
            try
            {
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
        }

        private static void OnErrorReceived(Exception ex, string message)
        {
            Logger.LogError("Socket error: " + message);
            if (ex != null) Logger.LogError(ex.GetBaseException().ToString());
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