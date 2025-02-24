using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Kitchen;
using KitchenLib;
using KitchenLib.Logging; 
using KitchenMods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KitchenData; 
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using KitchenDecorOnDemand;

namespace PlateupAP
{
    // Custom converter for JSON settings.
    public class SafeStringListConverter : JsonConverter<List<string>>
    {
        public override List<string> ReadJson(JsonReader reader, Type objectType, List<string> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            var list = new List<string>();
            if (token.Type == JTokenType.Array)
            {
                foreach (var item in token)
                {
                    string str = item.ToString();
                    if (str == "Disabled")
                        continue;
                    list.Add(str);
                }
            }
            else if (token.Type == JTokenType.String)
            {
                string str = token.ToString();
                if (str != "Disabled")
                    list.Add(str);
            }
            return list;
        }
        public override void WriteJson(JsonWriter writer, List<string> value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }

    public class ApplianceWrapper : MonoBehaviour
    {
        public CCreateAppliance Appliance;
        public void InitAppliance()
        {
            Appliance = new CCreateAppliance();
        }
    }

    public class PositionWrapper : MonoBehaviour
    {
        public void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }
    }

    public class Mod : BaseMod, IModSystem
    {
        public const string MOD_GUID = "com.caz.plateupap";
        public const string MOD_NAME = "PlateupAP";
        public const string MOD_VERSION = "0.1.0";
        public const string MOD_AUTHOR = "Caz";
        public const string MOD_GAMEVERSION = ">=1.1.9";

        internal static AssetBundle Bundle = null;
        // Fully qualify the logger to avoid ambiguity.
        internal static KitchenLib.Logging.KitchenLogger Logger;

        // Archipelago connection fields.
        private ArchipelagoSession session;
        private string ip = "archipelago.gg";
        private int port = 52218;
        private string playerName = "Caz";
        private string password = ""; // Optional.

        // Reconnection control.
        private bool connectionSuccessful = false;
        private float reconnectDelay = 10f;
        private float lastAttemptTime = -100f;

        // Day cycle fields.
        private bool dayPhase;
        private bool prepPhase;
        private int lastDay = 0;
        private const int dayID = 100000;
        private int timesFranchised = 0;
        private bool loggedDayTransition = false;
        private bool loggedPrepTransition = false;
        private bool itemsEventSubscribed = false;

        // Mapping from received item names to in-game GDO IDs (from KitchenData).
        private readonly Dictionary<string, int> itemToGDO = new Dictionary<string, int>()
        {
            { "Hob", 1001 },
            { "Sink", 1002 },
            { "Counter", 1003 },
            { "Dining Table", 1004 }
        };

        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly())
        {
        }

        protected override void OnInitialise()
        {
            Logger = InitLogger();
            Logger.LogWarning($"{MOD_GUID} v{MOD_VERSION} in use!");
            Newtonsoft.Json.JsonConvert.DefaultSettings = () =>
                new Newtonsoft.Json.JsonSerializerSettings { Converters = new List<JsonConverter> { new SafeStringListConverter() } };
        }

        protected override void OnUpdate()
        {
            if (!connectionSuccessful && HasSingleton<SFranchiseMarker>())
            {
                if (UnityEngine.Time.time - lastAttemptTime >= reconnectDelay)
                {
                    lastAttemptTime = UnityEngine.Time.time;
                    TryConnectToArchipelago();
                }
            }
            if (connectionSuccessful && HasSingleton<SKitchenMarker>())
            {
                UpdateDayCycle();
                CheckReceivedItems();
            }
        }

        // Subscribe to the received items event.
        private void CheckReceivedItems()
        {
            if (!itemsEventSubscribed)
            {
                session.Items.ItemReceived += OnItemReceived;
                itemsEventSubscribed = true;
            }
        }

        // When a check (item) is received via Archipelago, spawn a blueprint for that item.
        private void OnItemReceived(ReceivedItemsHelper helper)
        {
            // Dequeue the new item immediately.
            helper.DequeueItem();

            // Get the most recent item from the AllItemsReceived collection.
            var networkItem = session.Items.AllItemsReceived.LastOrDefault();
            if (networkItem == null)
            {
                Logger.LogInfo("No received item found in AllItemsReceived.");
                return;
            }

            // Use dynamic to access the "Item" property.
            long itemId = ((dynamic)networkItem).Item;
            string itemName = session.Items.GetItemName(itemId) ?? $"Item: {itemId}";
            if (string.IsNullOrEmpty(itemName))
            {
                Logger.LogInfo("Received item has no name.");
                return;
            }

            if (itemToGDO.TryGetValue(itemName, out int gdoID))
            {
                // Request a spawn via the PlateUpDecorOnDemand system.
                // This spawns a blueprint at the player's location.
                SpawnRequestSystem.Request<Appliance>(gdoID, SpawnPositionType.Player, 0, SpawnApplianceMode.Blueprint);
            }
            else
            {
                Logger.LogInfo($"Received unknown item: {itemName}");
            }
        }

        // Update the day cycle.
        private void UpdateDayCycle()
        {
            if (connectionSuccessful && session != null)
            {
                bool isDayStart = HasSingleton<SIsDayFirstUpdate>();
                bool isPrepStart = HasSingleton<SIsNightFirstUpdate>();

                if (isPrepStart)
                {
                    dayPhase = false;
                    prepPhase = true;
                    if (!loggedPrepTransition)
                    {
                        Logger.LogInfo("It's time to prep for the next day!");
                        loggedPrepTransition = true;
                        loggedDayTransition = false;
                    }
                }
                if (isDayStart)
                {
                    lastDay++;
                    prepPhase = false;
                    dayPhase = true;
                    if (!loggedDayTransition)
                    {
                        Logger.LogInfo("Transitioning from night to day, advancing day count...");
                        loggedDayTransition = true;
                        loggedPrepTransition = false;
                        if (lastDay < 15)
                        {
                            session.Locations.CompleteLocationChecks(dayID + lastDay);
                            int presentdayID = dayID + lastDay;
                            Logger.LogInfo("Day Logged " + lastDay + " with ID " + presentdayID);
                        }
                        else if (lastDay >= 16)
                        {
                            Logger.LogInfo("You franchised!");
                            timesFranchised++;
                            lastDay = 0;
                        }
                    }
                }
            }
        }

        private void TryConnectToArchipelago()
        {
            try
            {
                Logger.LogInfo("Attempting connection to Archipelago...");
                string connectionUrl = $"wss://{ip}:{port}";
                session = ArchipelagoSessionFactory.CreateSession(connectionUrl);
                if (session == null)
                {
                    Logger.LogError("Failed to create session. Session is null.");
                    return;
                }
                session.Socket.ErrorReceived += Socket_ErrorReceived;
                session.Socket.SocketOpened += Socket_SocketOpened;
                session.Socket.SocketClosed += Socket_SocketClosed;
                if (!string.IsNullOrEmpty(password))
                {
                    Logger.LogWarning("Password provided, but password support is not implemented. Proceeding without using the password.");
                }
                var result = session.TryConnectAndLogin("plateup", playerName, ItemsHandlingFlags.AllItems);
                if (result is LoginSuccessful)
                {
                    connectionSuccessful = true;
                    Logger.LogInfo($"Successfully connected to Archipelago as slot '{playerName}'.");
                }
                else if (result is LoginFailure failure)
                {
                    Logger.LogError($"Connection failed: {string.Join(", ", failure.Errors)}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception during connection attempt: {ex}");
            }
        }

        private void Socket_ErrorReceived(Exception e, string message)
        {
            Logger.LogInfo($"Socket Error: {message}");
            Logger.LogInfo($"Socket Exception: {e.Message}");
            if (e.StackTrace != null)
            {
                foreach (var line in e.StackTrace.Split('\n'))
                    Logger.LogInfo($"    {line}");
            }
            else
            {
                Logger.LogInfo("    No stacktrace provided");
            }
        }
        private void Socket_SocketOpened()
        {
            Logger.LogInfo($"Socket opened to: {session.Socket.Uri}");
        }
        private void Socket_SocketClosed(string reason)
        {
            Logger.LogInfo($"Socket closed: {reason}");
        }
    }
}
