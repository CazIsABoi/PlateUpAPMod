using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using KitchenLib.Logging;

namespace KitchenPlateupAP
{
    public static class ArchipelagoConnectionManager
    {
        private static readonly KitchenLogger Logger = new KitchenLogger("ArchipelagoConnectionManager");
        private static readonly object _stateLock = new object();
        private static CancellationTokenSource _cts;
        private static ArchipelagoSession _session;
        private static string _currentAttemptUri;

        public static ArchipelagoSession Session
        {
            get { lock (_stateLock) return _session; }
            private set { lock (_stateLock) _session = value; }
        }

        public static bool ConnectionSuccessful { get; private set; }
        public static bool IsConnecting { get; private set; }
        public static bool IsReconnecting { get; private set; }

        public static Dictionary<string, object> SlotData;
        public static int SlotIndex;

        public static event Action Connected;
        public static event Action<string> ConnectionFailed;
        public static event Action<string> Disconnected;

        private const string GameName = "plateup";

        [Obsolete("Use StartConnect(...) or await TryConnectAsync(...). The sync wrapper can deadlock Unity.")]
        public static void TryConnect(string host, int port, string playerName, string password)
        {
            // Intentionally changed: fire-and-forget instead of blocking GetResult()
            _ = TryConnectAsync(host, port, playerName, password, ItemsHandlingFlags.AllItems);
        }

        // Convenience method for UI button (non-async handler)
        public static void StartConnect(string host, int port, string playerName, string password,
                                        ItemsHandlingFlags flags = ItemsHandlingFlags.AllItems,
                                        bool autoReconnect = false)
        {
            _ = TryConnectAsync(host, port, playerName, password, flags, enableReconnect: autoReconnect);
        }

        public static async Task TryConnectAsync(
            string host,
            int port,
            string playerName,
            string password,
            ItemsHandlingFlags itemsHandling = ItemsHandlingFlags.AllItems,
            int maxAttemptsPerProtocol = 1,
            bool enableReconnect = false,
            int maxTotalAttempts = 4,
            TimeSpan? attemptTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                Logger.LogError("Player (slot) name cannot be empty.");
                return;
            }

            lock (_stateLock)
            {
                if (IsConnecting)
                {
                    Logger.LogInfo("Connection attempt already in progress.");
                    return;
                }
                IsConnecting = true;
                ConnectionSuccessful = false;
            }

            CleanupSession();
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            attemptTimeout ??= TimeSpan.FromSeconds(15);

            string[] protocolsToTry = BuildProtocolList(host);
            int totalAttempts = 0;
            Exception lastException = null;

            try
            {
                foreach (var protocol in protocolsToTry)
                {
                    for (int attempt = 1; attempt <= maxAttemptsPerProtocol; attempt++)
                    {
                        if (totalAttempts >= maxTotalAttempts) break;
                        totalAttempts++;

                        if (ct.IsCancellationRequested) return;

                        string uri = protocol + host.Trim().TrimEnd('/') + ":" + port + "/";
                        _currentAttemptUri = uri;
                        Logger.LogInfo($"[Attempt {totalAttempts}] Connecting to {uri} as '{playerName}' (ItemsHandling={itemsHandling}).");

                        ArchipelagoSession localSession;
                        try
                        {
                            localSession = ArchipelagoSessionFactory.CreateSession(uri);
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            Logger.LogError($"Failed creating session for {uri}: {ex.GetBaseException().Message}");
                            continue;
                        }

                        Session = localSession;
                        AttachSocketHandlers(localSession);

                        LoginResult loginResult = null;

                        try
                        {
                            Logger.LogInfo("Starting TryConnectAndLogin (background)...");
                            loginResult = await RunWithTimeout(
                                () => localSession.TryConnectAndLogin(GameName, playerName, itemsHandling, password: password),
                                attemptTimeout.Value,
                                ct).ConfigureAwait(false);
                            Logger.LogInfo("TryConnectAndLogin returned.");
                        }
                        catch (OperationCanceledException)
                        {
                            Logger.LogError("Connection attempt canceled.");
                            if (!ConnectionSuccessful && ReferenceEquals(Session, localSession))
                                Session = null;
                            return;
                        }
                        catch (TimeoutException)
                        {
                            Logger.LogError("Login attempt timed out.");
                            if (ReferenceEquals(Session, localSession) && !ConnectionSuccessful)
                                Session = null;
                            DetachSocketHandlers(localSession);
                            continue;
                        }
                        catch (Exception ex)
                        {
                            lastException = ex;
                            Logger.LogError($"Unexpected error during login: {ex.GetBaseException().Message}");
                            if (ReferenceEquals(Session, localSession) && !ConnectionSuccessful)
                                Session = null;
                            DetachSocketHandlers(localSession);
                            continue;
                        }

                        if (loginResult == null)
                        {
                            Logger.LogError("Login result was null (unknown failure).");
                            if (ReferenceEquals(Session, localSession) && !ConnectionSuccessful)
                                Session = null;
                            DetachSocketHandlers(localSession);
                            continue;
                        }

                        if (loginResult.Successful)
                        {
                            var success = (LoginSuccessful)loginResult;
                            ConnectionSuccessful = true;

                            SlotIndex = success.Slot;
                            SlotData = success.SlotData;

                            Logger.LogInfo($"Successfully connected to {uri} (Slot {SlotIndex}, Name '{playerName}').");
                            lock (_stateLock) IsConnecting = false;

                            // Dispatch events (no Unity object access here)
                            SafeInvokeConnected();

                            try
                            {
                                // If OnSuccessfulConnect touches Unity objects, ensure it runs on main thread (add a dispatcher if needed).
                                Mod.Instance.OnSuccessfulConnect();
                            }
                            catch (Exception callbackEx)
                            {
                                Logger.LogError("OnSuccessfulConnect callback threw: " + callbackEx.GetBaseException().Message);
                            }

                            if (enableReconnect)
                            {
                                _ = StartReconnectMonitorAsync(host, port, playerName, password, itemsHandling, ct);
                            }

                            return;
                        }
                        else
                        {
                            var failure = (LoginFailure)loginResult;
                            string formatted = FormatFailure(uri, playerName, failure);
                            Logger.LogError(formatted);

                            var errors = failure.Errors ?? Array.Empty<string>();
                            lastException = new Exception(string.Join("; ", errors));

                            if (ReferenceEquals(Session, localSession))
                                Session = null;

                            DetachSocketHandlers(localSession);
                        }
                    }
                }

                string finalMessage = "All connection attempts failed.";
                if (lastException != null)
                    finalMessage += " Last error: " + lastException.GetBaseException().Message;

                Logger.LogError(finalMessage);
                SafeInvokeConnectionFailed(finalMessage);
            }
            finally
            {
                lock (_stateLock)
                {
                    if (!ConnectionSuccessful)
                        IsConnecting = false;
                }
            }
        }

        private static async Task StartReconnectMonitorAsync(
            string host,
            int port,
            string playerName,
            string password,
            ItemsHandlingFlags flags,
            CancellationToken external)
        {
            int attempt = 0;
            TimeSpan baseDelay = TimeSpan.FromSeconds(5);

            while (!external.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), external).ConfigureAwait(false);
                if (external.IsCancellationRequested) break;

                if (Session == null || !ConnectionSuccessful)
                {
                    if (IsConnecting) continue;

                    attempt++;
                    TimeSpan delay = TimeSpan.FromMilliseconds(
                        Math.Min(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1), 30000));

                    Logger.LogInfo($"Reconnection attempt #{attempt} in {delay.TotalSeconds:0.#}s...");
                    IsReconnecting = true;
                    try
                    {
                        await Task.Delay(delay, external).ConfigureAwait(false);
                        if (external.IsCancellationRequested) break;

                        await TryConnectAsync(host, port, playerName, password, flags, maxAttemptsPerProtocol: 1, enableReconnect: true)
                            .ConfigureAwait(false);
                        if (ConnectionSuccessful)
                        {
                            Logger.LogInfo("Reconnection successful.");
                            IsReconnecting = false;
                            attempt = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private static async Task<T> RunWithTimeout<T>(Func<T> func, TimeSpan timeout, CancellationToken ct)
        {
            var task = Task.Run(func, ct);
            var finished = await Task.WhenAny(task, Task.Delay(timeout, ct)).ConfigureAwait(false);
            if (finished != task)
                throw new TimeoutException("Operation exceeded timeout of " + timeout);
            ct.ThrowIfCancellationRequested();
            return task.Result;
        }

        private static void AttachSocketHandlers(ArchipelagoSession session)
        {
            session.Socket.SocketOpened += OnSocketOpened;
            session.Socket.SocketClosed += OnSocketClosed;
            session.Socket.ErrorReceived += OnSocketError;
        }

        private static void DetachSocketHandlers(ArchipelagoSession session)
        {
            try
            {
                session.Socket.SocketOpened -= OnSocketOpened;
                session.Socket.SocketClosed -= OnSocketClosed;
                session.Socket.ErrorReceived -= OnSocketError;
            }
            catch
            {
            }
        }

        private static void CleanupSession()
        {
            var old = Session;
            if (old != null)
            {
                try
                {
                    DetachSocketHandlers(old);
                    old.Socket?.DisconnectAsync();
                }
                catch { }
            }
            Session = null;
        }

        private static string FormatFailure(string uri, string playerName, LoginFailure failure)
        {
            var msg = $"Failed to connect to {uri} as '{playerName}'.";
            if (failure.Errors != null)
            {
                foreach (var e in failure.Errors)
                    msg += "\n  - " + e;
            }
            if (failure.ErrorCodes != null)
            {
                foreach (var code in failure.ErrorCodes)
                    msg += "\n  - Code: " + code;
            }
            return msg;
        }

        private static string[] BuildProtocolList(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return new[] { "wss://", "ws://" };

            // If caller already included a protocol, return sentinel empty string so we don't prepend another
            if (host.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { string.Empty };
            }

            // Default preference: secure first, then insecure fallback
            return new[] { "wss://", "ws://" };
        }

        #region Socket Event Handlers

        private static void OnSocketOpened()
        {
            // Use Session if present; fall back to last attempt URI
            var uriText = Session?.Socket?.Uri?.ToString() ?? _currentAttemptUri ?? "<unknown>";
            Logger.LogInfo("Socket opened (pre-login possible): " + uriText);
        }

        private static void OnSocketClosed(string reason)
        {
            Logger.LogInfo("Socket closed: " + reason);
            if (ConnectionSuccessful)
            {
                ConnectionSuccessful = false;
                SafeInvokeDisconnected(reason);
            }
        }

        private static void OnSocketError(Exception ex, string message)
        {
            Logger.LogError("Socket error: " + message);
            Logger.LogError("Exception: " + ex.GetBaseException().Message);
        }

        #endregion

        #region Safe Event Invokers

        private static void SafeInvokeConnected()
        {
            try { Connected?.Invoke(); } catch (Exception e) { Logger.LogError("Connected event handler error: " + e.Message); }
        }

        private static void SafeInvokeConnectionFailed(string msg)
        {
            try { ConnectionFailed?.Invoke(msg); } catch (Exception e) { Logger.LogError("ConnectionFailed event handler error: " + e.Message); }
        }

        private static void SafeInvokeDisconnected(string reason)
        {
            try { Disconnected?.Invoke(reason); } catch (Exception e) { Logger.LogError("Disconnected event handler error: " + e.Message); }
        }

        #endregion
    }
}