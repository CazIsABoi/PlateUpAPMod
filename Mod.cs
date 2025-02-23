using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Kitchen;
using KitchenLib;
using KitchenLib.Logging;
using KitchenLib.Logging.Exceptions;
using KitchenMods;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KitchenLogger = KitchenLib.Logging.KitchenLogger;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Localization;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;

namespace PlateupAP
{
    // Custom converter class for JSON settings.
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

    // Wrapper to allow adding appliance creation logic as a component.
    public class ApplianceWrapper : MonoBehaviour
    {
        public CCreateAppliance Appliance;

        public void InitAppliance()
        {
            // Instantiate and initialize your appliance logic.
            Appliance = new CCreateAppliance();
            // Call any initialization on Appliance if necessary.
        }
    }

    // Wrapper that sets a GameObject’s transform position.
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
        internal static KitchenLogger Logger;

        // Connection fields.
        private ArchipelagoSession session; 
        private string ip = "archipelago.gg";   
        private int port = 65253;  
        private string playerName = "Caz";
        private string password = ""; // Optional – currently not used.

        // Reconnection control.
        private bool connectionSuccessful = false;
        private float reconnectDelay = 10f;
        private float lastAttemptTime = -100f;

        // (These fields are assigned but not yet used in additional logic.)
        private bool dayPhase;
        private bool prepPhase;
        private int lastDay = 0;
        private const int dayID = 100000;
        private int timesFranchised = 0;
        private bool loggedDayTransition = false;
        private bool loggedPrepTransition = false;
        private bool itemsEventSubscribed = false;


        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly()) { }

        protected override void OnInitialise()
        {
            Logger = InitLogger();
            Logger.LogWarning($"{MOD_GUID} v{MOD_VERSION} in use!");

            Newtonsoft.Json.JsonConvert.DefaultSettings = () => new Newtonsoft.Json.JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new SafeStringListConverter() }
            };
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
                checkReceivedItems();
            }
        }

        // Subscribe to the item received event only once.
        private void checkReceivedItems() 
        {  
            if (!itemsEventSubscribed)
            {
                session.Items.ItemReceived += OnItemReceived;
                itemsEventSubscribed = true;
            }
        }

        // Event handler for received items.
private void OnItemReceived(ReceivedItemsHelper helper)
{
    var receivedItem = helper.PeekItem();
    if (receivedItem == null)
    {
        Logger.LogInfo("No received item available.");
        helper.DequeueItem();
        return;
    }

    string itemName = receivedItem.Name(GlobalLocalisation.Default, Permissions.All);
    if (string.IsNullOrEmpty(itemName))
    {
        Logger.LogInfo("Received item has no name.");
        helper.DequeueItem();
        return;
    }

    switch (itemName)
    {
        case "Hob":
            SpawnHob();
            break;
        case "Sink":
            SpawnSink();
            break;
        case "Counter":
            SpawnCounter();
            break;
        case "Dining Table":
            SpawnDiningTable();
            break;
        default:
            Logger.LogInfo($"Received unknown item: {itemName}");
            break;
    }
    helper.DequeueItem();
}
        // Spawn methods for each item.
        private void SpawnHob()
        {
            GameObject hobObject = new GameObject("Hob");
            var appliance = hobObject.AddComponent<ApplianceWrapper>();
            appliance.InitAppliance();
            var pos = hobObject.AddComponent<PositionWrapper>();
            pos.SetPosition(new Vector3(0f, 0f, 0f)); // Adjust position as needed.
            Logger.LogInfo("Spawned a Hob at position (0, 0, 0).");
        }

        private void SpawnSink()
        {
            GameObject sinkObject = new GameObject("Sink");
            var appliance = sinkObject.AddComponent<ApplianceWrapper>();
            appliance.InitAppliance();
            var pos = sinkObject.AddComponent<PositionWrapper>();
            pos.SetPosition(new Vector3(10f, 0f, 5f)); // Adjust position as needed.
            Logger.LogInfo("Spawned a Sink at position (10, 0, 5).");
        }

        private void SpawnCounter()
        {
            GameObject counterObject = new GameObject("Counter");
            var appliance = counterObject.AddComponent<ApplianceWrapper>();
            appliance.InitAppliance();
            var pos = counterObject.AddComponent<PositionWrapper>();
            pos.SetPosition(new Vector3(20f, 0f, 5f)); // Adjust position as needed.
            Logger.LogInfo("Spawned a Counter at position (20, 0, 5).");
        }

        private void SpawnDiningTable()
        {
            GameObject tableObject = new GameObject("Dining Table");
            var appliance = tableObject.AddComponent<ApplianceWrapper>();
            appliance.InitAppliance();
            var pos = tableObject.AddComponent<PositionWrapper>();
            pos.SetPosition(new Vector3(30f, 0f, 5f)); // Adjust position as needed.
            Logger.LogInfo("Spawned a Dining Table at position (30, 0, 5).");
        }

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

        // Socket event handlers.
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
