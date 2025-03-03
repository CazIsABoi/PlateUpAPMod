using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
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

        public static void TryConnect(string ip, int port, string playerName, string password)
        {
            if (IsConnecting)
                return;

            IsConnecting = true;
            string connectionUrl = $"wss://{ip}:{port}/";

            try
            {
                Session = ArchipelagoSessionFactory.CreateSession(connectionUrl);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error creating session: " + ex.GetBaseException().Message);
                IsConnecting = false;
                return;
            }

            Session.Socket.ErrorReceived += OnSocketError;
            Session.Socket.SocketOpened += OnSocketOpened;
            Session.Socket.SocketClosed += OnSocketClosed;

            LoginResult result;
            try
            {
                result = Session.TryConnectAndLogin("plateup", playerName, ItemsHandlingFlags.AllItems);
            }
            catch (Exception e)
            {
                result = new LoginFailure(e.GetBaseException().Message);
            }

            if (!result.Successful)
            {
                LoginFailure failure = (LoginFailure)result;
                string errorMessage = $"Failed to connect to {ip}:{port} as {playerName}:";
                foreach (string error in failure.Errors)
                {
                    errorMessage += $"\n    {error}";
                }
                foreach (ConnectionRefusedError error in failure.ErrorCodes)
                {
                    errorMessage += $"\n    {error}";
                }
                Logger.LogError(errorMessage);
                IsConnecting = false;
                return;
            }

            var loginSuccess = (LoginSuccessful)result;
            ConnectionSuccessful = true;
            Logger.LogInfo($"Successfully connected to Archipelago as slot '{playerName}'.");

            IsConnecting = false;
            SlotIndex = loginSuccess.Slot;
            SlotData = loginSuccess.SlotData;

            Mod.Instance.OnSuccessfulConnect();
        }

        private static void OnSocketError(Exception e, string message)
        {
            Logger.LogInfo("Socket Error: " + message);
            Logger.LogInfo("Socket Exception: " + e.Message);
            if (e.StackTrace != null)
            {
                foreach (var line in e.StackTrace.Split('\n'))
                {
                    Logger.LogInfo("    " + line);
                }
            }
            else
            {
                Logger.LogInfo("    No stacktrace provided");
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
