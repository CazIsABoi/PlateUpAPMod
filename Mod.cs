using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using HarmonyLib;
using Kitchen;
using KitchenLib;
using KitchenLib.Logging;
using KitchenLib.Utils;
using KitchenLib.References;
using KitchenMods;
using Newtonsoft.Json;
using KitchenData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Packets;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using KitchenDecorOnDemand;
using PreferenceSystem;
using PreferenceSystem.Event;
using PreferenceSystem.Menus;
using KitchenPlateupAP;


namespace KitchenPlateupAP
{
    public class ArchipelagoConfig
    {
        public string address { get; set; }
        public int port { get; set; }
        public string playername { get; set; }
        public string password { get; set; }
    }

    public class Mod : BaseMod, IModSystem
    {
        public const string MOD_GUID = "com.caz.plateupap";
        public const string MOD_NAME = "PlateupAP";
        public const string MOD_VERSION = "0.1.7";
        public const string MOD_AUTHOR = "Caz";
        public const string MOD_GAMEVERSION = ">=1.1.9";

        internal static AssetBundle Bundle = null;
        internal static KitchenLib.Logging.KitchenLogger Logger;
        private EntityQuery playersWithItems;
        private EntityQuery playerSpeedQuery;
        private EntityQuery applianceSpeedQuery;

        public static Mod Instance { get; private set; }

        private static ArchipelagoSession session => ArchipelagoConnectionManager.Session;
        private Archipelago.MultiClient.Net.BounceFeatures.DeathLink.DeathLinkService deathLinkService;
        private int deathLinkBehavior = 0; // Default to "Reset Run"
        private static int chosenGoal = 1;  // default to "Franchise Twice" if missing
        private static List<string> selectedDishes = new List<string>();
        object rawGoal = null;
        private static bool dishesMessageSent = false;
        private bool itemsQueuedThisLobby = false;
        int itemsKeptPerRun = 5;
        public static int RandomTrapCardCount = 0;
        bool deathLinkResetToLastStarPending = false;

        // Static day cycle and spawn state.
        private static int lastDay = 0;
        private int dayID = 100000;
        private int stars = 0;
        private int timesFranchised = 1;
        private int DishId;
        private bool firstCycleCompleted = false;
        private bool previousWasDay = false;
        bool inLobby = true;
        bool loseScreen = false;
        bool franchiseScreen = false;
        bool lost = false;
        bool franchised = false;
        private bool dayTransitionProcessed = false;

        private static bool itemsEventSubscribed = false;
        private static Queue<ItemInfo> spawnQueue = new Queue<ItemInfo>();

        // Flag to prevent repeated logging during a cycle.
        private static bool loggedCardThisCycle = false;
        private static bool prepLogDone = false;
        private static bool sessionNotInitLogged = false;
        private bool itemsSpawnedThisRun = false;

        //Modifying player values
        private static readonly float[] speedTiers = new float[] { 0.5f, 0.6f, 0.8f, 1f, 1.15f };
        private static int movementSpeedTier = 0;

        //Modifying Appliance Values
        public static readonly float[] applianceSpeedTiers = { 0.6f, 0.75f, 0.85f, 1.0f, 1.2f };
        public static int applianceSpeedTier = 0;


        // Set initial multipliers from the tiers:
        public static float movementSpeedMod = speedTiers[movementSpeedTier];
        public static float applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];

        public static class InputSourceIdentifier
        {
            public static int Identifier = 0;
        }

        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly())
        {
            Instance = this;
            Logger = InitLogger();
            Logger.LogWarning("Created instance");
        }

        private void RetrieveSlotData()
        {
            if (session == null)
                return; // Not connected

            var slotData = ArchipelagoConnectionManager.SlotData;  // Fetch globally stored slot data

            if (slotData != null)
            {
                Logger.LogInfo($"[PlateupAP] Full Slot Data: {JsonConvert.SerializeObject(slotData, Formatting.Indented)}");

                if (slotData.TryGetValue("selected_dishes", out object rawDishes))
                {
                    Logger.LogInfo($"[PlateupAP] Found selected_dishes in slot data: {rawDishes}");

                    try
                    {
                        // Deserialize the string into a list
                        selectedDishes = JsonConvert.DeserializeObject<List<string>>(rawDishes.ToString());

                        if (selectedDishes.Count > 0)
                        {
                            Logger.LogInfo($"[PlateupAP] Selected Dishes from Archipelago: {string.Join(", ", selectedDishes)}");
                            SendSelectedDishesMessage(); // Send the message once retrieved
                        }
                        else
                        {
                            Logger.LogWarning("[PlateupAP] Retrieved slot data, but selected_dishes is empty.");
                        }
                    }
                    catch (JsonReaderException ex)
                    {
                        Logger.LogError($"[PlateupAP] Error parsing selected_dishes JSON: {ex.Message}");
                    }

                    // Check if DeathLink is enabled
                    if (slotData.TryGetValue("death_link", out object rawDeathLink))
                    {
                        bool deathLinkEnabled = Convert.ToBoolean(rawDeathLink);
                        Logger.LogInfo($"[PlateupAP] DeathLink enabled: {deathLinkEnabled}");

                        if (deathLinkEnabled)
                        {
                            EnableDeathLink(); // Call function to activate DeathLink
                        }
                    }

                    // Retrieve DeathLink behavior setting
                    if (slotData.TryGetValue("death_link_behavior", out object rawBehavior))
                    {
                        deathLinkBehavior = Convert.ToInt32(rawBehavior);
                        Logger.LogInfo($"[PlateupAP] DeathLink Behavior Set To: {deathLinkBehavior}");
                    }

                    // Retrieve Items Kept setting
                    if (slotData.TryGetValue("items_kept", out object rawItemsKept))
                    {
                        itemsKeptPerRun = Convert.ToInt32(rawItemsKept);
                        Logger.LogInfo($"[PlateupAP] Items Kept Per Run: {itemsKeptPerRun}");
                    }
                }
            }
        }

        private void SendSelectedDishesMessage()
        {
            if (session == null || selectedDishes == null || selectedDishes.Count == 0)
            {
                Logger.LogWarning("Session is null or selected dishes list is empty. Not sending.");
                return;
            }

            string message = $"Selected Dishes: {string.Join(", ", selectedDishes)}";
            Logger.LogInfo($"Sending message: {message}");
            session.Socket.SendPacket(new SayPacket { Text = message });
        }

        static PreferenceSystemManager PrefManager;

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            try
            {
                if (World != null)
                {
                    World.GetOrCreateSystem<ApplyApplianceSpeedModifierSystem>().Enabled = true;
                    Mod.Logger.LogInfo("ApplianceSpeedModifierSystem enabled.");
                }
                else
                {
                    Mod.Logger.LogError("World is null in OnPostActivate!");
                }

                if (PrefManager == null)
                {
                    PrefManager = new PreferenceSystemManager(MOD_GUID, MOD_NAME);
                }

                if (ArchipelagoConnectionManager.ConnectionSuccessful)
                {
                    RetrieveSlotData();
                    ProcessAllReceivedItems();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlateupAP] Error in OnPostActivate: {ex.Message}\n{ex.StackTrace}");
            }

            PrefManager = new PreferenceSystemManager(MOD_GUID, MOD_NAME);
            PrefManager
                .AddLabel("Archipelago Configuration")
                .AddInfo("Create or load configuration for the Archipelago connection")
                .AddButton("Create Config", (int _) =>
                {
                    string folder = Path.Combine(Application.persistentDataPath, "PlateUpAPConfig");
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string path = Path.Combine(folder, "archipelago_config.json");
                    ArchipelagoConfig defaultConfig = new ArchipelagoConfig
                    {
                        address = "archipelago.gg",
                        port = 0,
                        playername = "",
                        password = ""
                    };
                    string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                    File.WriteAllText(path, json);
                    Debug.Log("Created config file at: " + path);
                })
                .AddButton("Connect", (int _) =>
                {
                    string folder = Path.Combine(Application.persistentDataPath, "PlateUpAPConfig");
                    string path = Path.Combine(folder, "archipelago_config.json");
                    if (!File.Exists(path))
                    {
                        Debug.LogError("Config file not found at: " + path);
                        return;
                    }
                    string json = File.ReadAllText(path);
                    ArchipelagoConfig config = JsonConvert.DeserializeObject<ArchipelagoConfig>(json);
                    if (string.IsNullOrEmpty(config.address))
                    {
                        Debug.LogError("Config file does not contain a valid address.");
                        return;
                    }
                    UpdateArchipelagoConfig(config);
                });

            if (ArchipelagoConnectionManager.ConnectionSuccessful)
            {

            }

            PrefManager.RegisterMenu(PreferenceSystemManager.MenuType.MainMenu);
            PrefManager.RegisterMenu(PreferenceSystemManager.MenuType.PauseMenu);
        }

        protected override void OnInitialise()
        {
            Logger = InitLogger();
            Logger.LogWarning($"{MOD_GUID} v{MOD_VERSION} in use!");
            var harmony = new Harmony("com.caz.plateupap.patch");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            JsonConvert.DefaultSettings = null;
            Mod.Logger.LogInfo("DishCardReadingSystem initialised.");
            playersWithItems = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CItemHolder)));
            playerSpeedQuery = GetEntityQuery(new QueryHelper().All(typeof(CPlayer)));
            applianceSpeedQuery = GetEntityQuery(new QueryHelper().All(typeof(CApplianceSpeedModifier)));
        }

        public void UpdateArchipelagoConfig(ArchipelagoConfig config)
        {
            // Use static connection manager.
            ArchipelagoConnectionManager.TryConnect(config.address, config.port, config.playername, config.password);
        }

        public void OnSuccessfulConnect()
        {
            if (ArchipelagoConnectionManager.ConnectionSuccessful)
            {
                RetrieveSlotData(); // Fetch slot data

                Logger.LogInfo("[Archipelago] Re-processing all previously received items...");
                ProcessAllReceivedItems();

                if (!dishesMessageSent && selectedDishes.Count > 0)
                {
                    SendSelectedDishesMessage();
                    dishesMessageSent = true;
                    Logger.LogInfo("Selected dishes message sent successfully.");
                }
                else if (selectedDishes.Count == 0)
                {
                    Logger.LogWarning("Tried to send selected dishes, but the list is empty.");
                }
                else
                {
                    Logger.LogWarning("Skipping duplicate dish message.");
                }
            }
        }


        private void EnableDeathLink()
        {
            if (session == null)
            {
                Logger.LogError("Cannot enable DeathLink, session is null.");
                return;
            }

            if (deathLinkService == null) // Prevent duplicate instances
            {
                deathLinkService = session.CreateDeathLinkService();
                deathLinkService.EnableDeathLink();
                deathLinkService.OnDeathLinkReceived += HandleDeathLinkEvent;

                Logger.LogInfo("[PlateupAP] DeathLink service enabled and event listener registered.");
            }
        }

        private void HandleDeathLinkEvent(DeathLink deathLink)
        {
            if (session == null || session.Socket == null)
            {
                Logger.LogError("[PlateupAP] DeathLink received, but session or socket is null. Cannot process.");
                return;
            }

            Logger.LogWarning($"[PlateupAP] DeathLink received! Cause: {deathLink.Source}");

            if (deathLinkBehavior == 0) // Full Reset
            {
                Logger.LogWarning("[PlateupAP] Player chose to fully reset the run due to DeathLink.");
                Entity entity = base.EntityManager.CreateEntity(typeof(SGameOver), typeof(CGamePauseBlock));
                Set(entity, new SGameOver
                {
                    Reason = LossReason.Patience
                });
            }
            else if (deathLinkBehavior == 1) // Reset to Last Star
            {
                Logger.LogWarning("[PlateupAP] Player chose to reset to the last earned star due to DeathLink.");

                deathLinkResetToLastStarPending = true;
            }
        }

        private void SendDeathLink()
        {
            if (deathLinkService != null)
            {
                string playerName = session.Players.GetPlayerAlias(session.ConnectionInfo.Slot);

                var deathLink = new DeathLink(playerName, "Player died in PlateUp!");
                deathLinkService.SendDeathLink(deathLink);

                Logger.LogInfo($"[PlateupAP] DeathLink event sent by player {playerName}.");
            }
        }

        private void ResetToLastStar()
        {
            Logger.LogInfo("[PlateupAP] Attempting to reset to last star...");

            var day = GetOrDefault<SDay>();

            Logger.LogInfo($"[PlateupAP] Current day: {day.Day}, Stars: {stars}");

            if (stars > 0 && day.Day > 1)
            {
                // Find the last earned star (last multiple of 3)
                int rollbackDays = day.Day % 3;
                int newDay = day.Day - rollbackDays;

                if (newDay < 1)
                {
                    Logger.LogWarning($"[PlateupAP] Calculated rollback resulted in invalid day {newDay}, setting to 1.");
                    newDay = 1;
                }

                Logger.LogInfo($"[PlateupAP] Rolling back to last star: {newDay}");
                day.Day = newDay;
                Set(day);

                Logger.LogInfo($"[PlateupAP] Reset to last earned star. New day: {day.Day}, Previous day: {lastDay}");
                lastDay = newDay;
            }
            else
            {
                Logger.LogWarning("[PlateupAP] No stars earned or already at day 1, resetting fully instead.");
                Entity entity = base.EntityManager.CreateEntity(typeof(SGameOver), typeof(CGamePauseBlock));
                Set(entity, new SGameOver
                {
                    Reason = LossReason.Patience
                });
            }
        }

        private Dictionary<Entity, float> slowEffectMultipliers = new Dictionary<Entity, float>();

        public float GetPlayerSpeedMultiplier(Entity player)
        {
            if (slowEffectMultipliers.ContainsKey(player))
            {
                return slowEffectMultipliers[player];
            }
            return 1.0f; // Default to normal speed
        }


        protected override void OnUpdate()
        {
            franchiseScreen = HasSingleton<SFranchiseBuilderMarker>();
            loseScreen = HasSingleton<SGameOver>();
            inLobby = HasSingleton<SFranchiseMarker>();
            if(HasSingleton<SKitchenMarker>())
            {
                UpdateDayCycle();
                CheckReceivedItems();
            }

            if (session == null || session.Locations == null)
            {
                return;
            }

            if (franchiseScreen && !franchised)
            {
                Logger.LogInfo("You franchised!");
                HandleGameReset();
                lastDay = 0;
                timesFranchised++;
                dayID = 100000 * timesFranchised;
                session.Locations.CompleteLocationChecks(dayID);
                franchised = true;

                Logger.LogInfo($"User has franchised {timesFranchised - 1} times. Goal is {chosenGoal + 1} franchises...");

                if ((timesFranchised - 1) == (chosenGoal + 1))
                {
                    Logger.LogInfo("Franchise goal reached! Sending goal complete.");
                    SendGoalComplete();
                }
            }

            else if (loseScreen && !lost)
            {
                Logger.LogInfo("You Lost the Run!");
                HandleGameReset();
                lastDay = 0;
                session.Locations.CompleteLocationChecks(100000);
                lost = true;

                if (deathLinkService != null)
                {
                    SendDeathLink();
                }

            }

            else if (inLobby && !itemsQueuedThisLobby)
            {
                firstCycleCompleted = false;
                previousWasDay = false;
                franchised = false;
                lost = false;
                stars = 0;
                lastDay = 0;

                Logger.LogInfo("[Lobby] Entered lobby. Preparing to queue items for next run...");

                if (spawnQueue.Count == 0)
                {
                    QueueItemsFromReceivedPool(itemsKeptPerRun); 
                    Logger.LogInfo($"[Lobby] {spawnQueue.Count} items queued for next run.");
                }
                else
                {
                    Logger.LogInfo("[Lobby] Items are already queued. Skipping queueing.");
                }

                itemsQueuedThisLobby = true;
            }

            if (deathLinkResetToLastStarPending)
            {
                deathLinkResetToLastStarPending = false;
                if (!HasSingleton<SDay>())
                {
                    Logger.LogError("[PlateupAP] SDay singleton not found. Cannot do star reset.");
                }
                else
                {
                    ResetToLastStar();
                }
            }

            // --- Dish Card Reading Logic ---
            // Create the query for player entities with CPlayer and CItemHolder.
            EntityQuery playersWithItems = GetEntityQuery(new QueryHelper().All(typeof(CPlayer), typeof(CItemHolder)));
                using var playerEntities = playersWithItems.ToEntityArray(Allocator.Temp);

                for (int i = 0; i < playerEntities.Length; i++)
                {
                    Entity player = playerEntities[i];
                    CItemHolder holder = EntityManager.GetComponentData<CItemHolder>(player);

                    if (holder.HeldItem != Entity.Null)
                    {
                        Entity heldItem = holder.HeldItem;
                        if (EntityManager.HasComponent<CDishChoice>(heldItem))
                        {
                            CDishChoice dishChoice = EntityManager.GetComponentData<CDishChoice>(heldItem);
                            int newDishID = dishChoice.Dish; // Get the new selected dish ID

                            // Check if the dish has changed while in the lobby
                            if (inLobby && newDishID != DishId && newDishID != 0)
                            {
                                DishId = newDishID; // Update stored dish ID
                                loggedCardThisCycle = false;

                                // Retrieve the Dish game data object.
                                Dish dishData = (Dish)GDOUtils.GetExistingGDO(DishId);
                                Logger.LogInfo($"New selected dish in HQ: {dishData.Name}");

                                if (ProgressionMapping.dishDictionary.TryGetValue(DishId, out string dishName) && !loggedCardThisCycle)
                                {
                                    Logger.LogInfo($"Updated dish (via dictionary): {dishName}");
                                    loggedCardThisCycle = true;
                                }
                                else if (!loggedCardThisCycle)
                                {
                                    Logger.LogInfo($"Dish with ID {DishId} not found in dictionary; using game data: {dishData.Name}");
                                    loggedCardThisCycle = true;
                                }
                            }
                            break;
                        }
                    }
                }   
        ApplySpeedModifiers();
        ApplyApplianceSpeedModifiers();
        }

        // Spawning Items
        private void CheckReceivedItems()
        {
            if (session == null || session.Items == null)
            {
                if (!sessionNotInitLogged)
                {
                    Logger.LogError("Session items not yet initialized.");
                    sessionNotInitLogged = true;
                }
                return;
            }
            // Reset the flag when session items become available.
            sessionNotInitLogged = false;

            if (!itemsEventSubscribed)
            {
                session.Items.ItemReceived += new ReceivedItemsHelper.ItemReceivedHandler(OnItemReceived);
                itemsEventSubscribed = true;
            }
        }

        private static List<int> receivedItemPool = new List<int>();

        private void OnItemReceived(ReceivedItemsHelper helper)
        {
            ItemInfo info = helper.DequeueItem();
            int checkId = (int)info.ItemId;
            Logger.LogInfo($"[OnItemReceived] Received check ID: {checkId}");

            // 1) Check if it's a TRAP
            if (ProgressionMapping.trapDictionary.ContainsKey(checkId))
            {
                Logger.LogWarning($"[OnItemReceived] Received TRAP: {ProgressionMapping.trapDictionary[checkId]}!");
                ApplyTrapEffect(checkId);
                return; // Do not add to pool or queue
            }

            // 2) Check if it's a SPEED UPGRADE (player or appliance) from the same dictionary
            if (ProgressionMapping.speedUpgradeMapping.TryGetValue(checkId, out string upgradeName))
            {
                if (upgradeName == "Speed Upgrade Player")
                {
                    // INCREMENT player speed tier
                    if (movementSpeedTier < speedTiers.Length - 1)
                    {
                        movementSpeedTier++;
                        movementSpeedMod = speedTiers[movementSpeedTier];
                        Logger.LogInfo($"[OnItemReceived] Player speed upgraded to tier {movementSpeedTier}. Multiplier = {movementSpeedMod}");
                    }
                    else
                    {
                        Logger.LogInfo("[OnItemReceived] Player speed already at max tier.");
                    }

                    Logger.LogInfo("[OnItemReceived] Skipping player speed item for next run.");
                    return;
                }
                else if (upgradeName == "Speed Upgrade Appliance")
                {
                    Mod.Instance.IncreaseApplianceSpeedTier();
                    Logger.LogInfo($"[OnItemReceived] Appliance speed item used (ID {checkId}). Skipping for next run.");
                    return;
                }
            }

            receivedItemPool.Add(checkId);
            Logger.LogInfo($"[OnItemReceived] Item ID {checkId} added to receivedItemPool.");

            Logger.LogInfo($"[OnItemReceived] Queuing item ID: {checkId}");

            if (!spawnQueue.Any(x => (int)x.ItemId == checkId))
            {
                bool currentlyPrep = HasSingleton<SIsNightTime>();
                if (currentlyPrep)
                {
                    Logger.LogInfo($"[OnItemReceived] Currently in prep phase; queueing item ID: {checkId} for immediate spawn.");
                    spawnQueue.Enqueue(info);
                }
                else
                {
                    Logger.LogInfo($"[OnItemReceived] Item ID {checkId} is queued for the next run.");
                    spawnQueue.Enqueue(info);
                }
            }
            else
            {
                Logger.LogInfo($"[OnItemReceived] Item ID {checkId} is already in spawnQueue. Skipping duplicate.");
            }
        }

        private ItemInfo CreateItemInfoForQueue(int itemId)
        {
            Logger.LogInfo($"Creating ItemInfo for Item ID: {itemId}");

            // Initialize NetworkItem using an object initializer
            var networkItem = new NetworkItem
            {
                Item = itemId,
                Location = 0, // Set appropriate Location ID
                Player = 0    // Set appropriate Player ID
            };

            // Construct the ItemInfo object with the networkItem
            return new ItemInfo(networkItem, "", "", null, null);
        }

        private void ProcessAllReceivedItems()
        {
            if (session == null || session.Items == null)
            {
                Logger.LogError("Session items not yet initialized, cannot process received items.");
                return;
            }

            Logger.LogInfo($"[ProcessAllReceivedItems] Processing {session.Items.AllItemsReceived.Count} past items...");

            foreach (var item in session.Items.AllItemsReceived)
            {
                int itemId = (int)item.ItemId;

                // Check if it's a Speed Upgrade
                if (ProgressionMapping.speedUpgradeMapping.TryGetValue(itemId, out string upgradeType))
                {
                    if (upgradeType == "Speed Upgrade Player")
                    {
                        if (movementSpeedTier < speedTiers.Length - 1)
                        {
                            movementSpeedTier++;
                            movementSpeedMod = speedTiers[movementSpeedTier];
                            Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Player Speed Upgrade. Tier: {movementSpeedTier} (Multiplier: {movementSpeedMod})");
                        }
                    }
                    else if (upgradeType == "Speed Upgrade Appliance")
                    {
                        if (applianceSpeedTier < applianceSpeedTiers.Length - 1)
                        {
                            applianceSpeedTier++;
                            applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];
                            Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Appliance Speed Upgrade. Tier: {applianceSpeedTier} (Multiplier: {applianceSpeedMod})");
                        }
                    }
                }
            }
        }


        private void HandleGameReset()
        {
            Logger.LogInfo("[PlateupAP] Handling game reset...");
            firstCycleCompleted = false;
            previousWasDay = false;
            franchised = false;
            lost = false;
            lastDay = 0;
            stars = 0;
            itemsQueuedThisLobby = false;
            itemsSpawnedThisRun = false;

            Logger.LogInfo("[PlateupAP] Cleared expired items from spawn queue.");

            Logger.LogInfo("[PlateupAP] Game reset complete. Ready for a new run.");
        }

        private void QueueItemsFromReceivedPool(int count)
        {
            if (session == null || session.Items == null)
            {
                Logger.LogError("Session or session items are null. Cannot retrieve received items.");
                return;
            }

            HashSet<int> trapIDs = new HashSet<int>(ProgressionMapping.trapDictionary.Keys);

            // Log all received items from Archipelago
            Logger.LogInfo("[QueueItemsFromReceivedPool] Total received items count: " + session.Items.AllItemsReceived.Count);

            if (session.Items.AllItemsReceived.Count == 0)
            {
                Logger.LogWarning("[QueueItemsFromReceivedPool] No items have been received in this session.");
                return;
            }

            // Correctly use item.ItemId from ItemInfo
            var receivedItems = session.Items.AllItemsReceived
                .Select(item => (int)item.ItemId)
                .Where(id =>
                    !ProgressionMapping.speedUpgradeMapping.ContainsKey(id) &&
                    !trapIDs.Contains(id)
                )
                .ToList();

            Logger.LogInfo("[QueueItemsFromReceivedPool] Non-speed, non-trap item count: " + receivedItems.Count);

            if (receivedItems.Count == 0)
            {
                Logger.LogWarning("[QueueItemsFromReceivedPool] No valid non-speed, non-trap items available to queue for next run.");
                return;
            }

            var random = new System.Random();
            var selectedItems = receivedItems.OrderBy(_ => random.Next()).Take(count).ToList();

            foreach (int itemId in selectedItems)
            {
                Logger.LogInfo("[QueueItemsFromReceivedPool] Queuing item ID " + itemId + " for next run.");
                spawnQueue.Enqueue(CreateItemInfoForQueue(itemId));
            }

            Logger.LogInfo("[QueueItemsFromReceivedPool] " + selectedItems.Count + " items added to spawn queue.");
        }

        private void QueueItemsForNextRun(int count)
        {
            if (receivedItemPool.Count == 0)
            {
                Logger.LogWarning("No items available to queue for next run.");
                return;
            }

            // Filter out speed upgrades
            var validItems = receivedItemPool.Where(id => !ProgressionMapping.speedUpgradeMapping.ContainsKey(id)).ToList();

            if (validItems.Count == 0)
            {
                Logger.LogWarning("All items in pool are speed upgrades. No items will be queued.");
                return;
            }

            // Randomly select 'count' items (or all available if less than count)
            var random = new System.Random();
            var selectedItems = validItems.OrderBy(_ => random.Next()).Take(count).ToList();

            foreach (int itemId in selectedItems)
            {
                if (!spawnQueue.Any(x => (int)x.ItemId == itemId))
                {
                    Logger.LogInfo($"Queuing item ID {itemId} for next run.");
                    spawnQueue.Enqueue(CreateItemInfoForQueue(itemId));
                }
                else
                {
                    Logger.LogInfo($"Item ID {itemId} already queued. Skipping.");
                }

            }
        }

        private void ProcessSpawn(ItemInfo info)
        {
            int checkId = (int)info.ItemId;

            // Check persistently with Archipelago if item is still valid
            string itemName = session.Items.GetItemName(checkId);
            if (string.IsNullOrEmpty(itemName))
            {
                Logger.LogWarning("[Spawn] Skipping expired or invalid item ID: " + checkId);
                return;
            }

            Logger.LogInfo("[Spawn] Processing item ID: " + checkId);

            // Handle speed upgrades
            if (ProgressionMapping.speedUpgradeMapping.ContainsKey(checkId))
            {
                string upgradeType = ProgressionMapping.speedUpgradeMapping[checkId];

                if (upgradeType == "Speed Upgrade Player" && movementSpeedTier < speedTiers.Length - 1)
                {
                    movementSpeedTier++;
                    movementSpeedMod = speedTiers[movementSpeedTier];
                    Logger.LogInfo(upgradeType + " applied. Movement speed now at tier " + movementSpeedTier + " (multiplier = " + movementSpeedMod + ").");
                }
                else if (upgradeType == "Speed Upgrade Appliance" && applianceSpeedTier < applianceSpeedTiers.Length - 1)
                {
                    applianceSpeedTier++;
                    applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];
                    Logger.LogInfo(upgradeType + " applied. Appliance speed now at tier " + applianceSpeedTier + " (multiplier = " + applianceSpeedMod + ").");
                }
                return;
            }

            // Handle regular item spawning
            if (ProgressionMapping.progressionToGDO.TryGetValue(checkId, out int gdoId))
            {
                SpawnPositionType positionType = SpawnPositionType.Door;
                SpawnApplianceMode spawnApplianceMode = SpawnApplianceMode.Blueprint;

                if (KitchenData.GameData.Main.TryGet<Appliance>(gdoId, out Appliance appliance))
                    SpawnRequestSystem.Request<Appliance>(gdoId, positionType, InputSourceIdentifier.Identifier, spawnApplianceMode);
                else if (KitchenData.GameData.Main.TryGet<Decor>(gdoId, out Decor decor))
                    SpawnRequestSystem.Request<Decor>(gdoId, positionType);
                else
                    Logger.LogWarning("GDO id " + gdoId + " does not correspond to a known Appliance or Decor.");
            }
            else
            {
                Logger.LogWarning("No mapping found for check id: " + checkId);
            }
        }

        //Traps
        private void ApplyTrapEffect(int trapId)
        {
            switch (trapId)
            {
                case 20000: // EVERYTHING IS ON FIRE
                    Logger.LogWarning("[Trap] EVERYTHING IS ON FIRE activated! Igniting appliances...");
                    IgniteAllAppliances();
                    break;

                case 20001: // Super Slow
                    Logger.LogWarning("[Trap] Super Slow activated! Reducing player speed...");
                    ApplySlowEffect();
                    break;

                case 20002: // Random Customer Card
                    Logger.LogWarning("[Trap] Random Customer Card triggered! Incrementing our card count...");
                    RandomTrapCardCount++;
                    Logger.LogInfo($"We’ve now received this RandomCard trap {RandomTrapCardCount} time(s).");

                    // If we are already in the kitchen scene, spawn one card *right now*:
                    if (HasSingleton<SKitchenMarker>())
                    {
                        Logger.LogInfo("[Trap] We are in the Kitchen, so spawning a random card immediately...");
                        SpawnRandomCustomerCard();
                    }
                    else
                    {
                        Logger.LogInfo("[Trap] Not in Kitchen yet, so no immediate card spawn. We'll spawn later.");
                    }
                    break;


                default:
                    Logger.LogWarning($"[Trap] Unknown trap ID {trapId} received.");
                    break;
            }
        }

        private void IgniteAllAppliances()
        {
            Logger.LogInfo("[Trap] Igniting all appliances...");

            EntityQuery applianceQuery = GetEntityQuery(new QueryHelper()
                .All(typeof(CAppliance))
                .None(typeof(CFire), typeof(CIsOnFire), typeof(CFireImmune)));

            using (var appliances = applianceQuery.ToEntityArray(Allocator.TempJob))
            {
                int count = appliances.Length;
                for (int i = 0; i < count; i++)
                {
                    EntityManager.AddComponent<CIsOnFire>(appliances[i]);
                }
            }
        }

        private Dictionary<Entity, float> originalSpeeds = new Dictionary<Entity, float>(); 
        private HashSet<Entity> activeSlowEffects = new HashSet<Entity>(); 

        private void ApplySlowEffect()
        {
            Logger.LogWarning("[Trap] Applying slow effect to players...");

            EntityQuery playerQuery = GetEntityQuery(ComponentType.ReadWrite<CPlayer>());
            using (var playerEntities = playerQuery.ToEntityArray(Allocator.TempJob))
            {
                int count = playerEntities.Length;
                for (int i = 0; i < count; i++)
                {
                    Entity player = playerEntities[i];

                    // Set a slow effect multiplier (25% of normal speed)
                    if (!slowEffectMultipliers.ContainsKey(player))
                    {
                        slowEffectMultipliers[player] = 0.25f;
                        Logger.LogInfo($"[Trap] Player {i} speed reduced.");
                    }

                    // Schedule speed restoration after 30 seconds
                    RestoreSpeedAfterDelay(player, 15);
                }
            }
        }

        private async void RestoreSpeedAfterDelay(Entity player, int delaySeconds)
        {
            await Task.Delay(delaySeconds * 1000);

            if (slowEffectMultipliers.ContainsKey(player))
            {
                slowEffectMultipliers.Remove(player);
                Logger.LogInfo($"[Trap] Player speed restored after {delaySeconds} seconds.");
            }
        }

        private void SpawnRandomCustomerCard()
        {
            if (!HasSingleton<SKitchenMarker>())
            {
                Logger.LogWarning("[Trap] Tried to spawn a random card, but we're not in the kitchen scene!");
                return;
            }

            var dict = ProgressionMapping.customerCardDictionary;
            if (dict.Count == 0)
            {
                Logger.LogWarning("[Trap] No customer cards available in the dictionary!");
                return;
            }

            List<int> keys = new List<int>(dict.Keys);
            int randomIndex = UnityEngine.Random.Range(0, keys.Count);
            int randomKey = keys[randomIndex];
            int unlockCardId = dict[randomKey];

            Entity entity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(entity, new CProgressionOption
            {
                ID = unlockCardId,
                FromFranchise = false
            });

            //EntityManager.AddComponent<CSkipShowingRecipe>(entity);

            EntityManager.AddComponent<CProgressionOption.Selected>(entity);

            Logger.LogInfo($"[Trap->RandomCard] Spawned random card key={randomKey}, unlockID={unlockCardId}");
        }

        // Checks and Day Cycle
        private void UpdateDayCycle()
        {
            if (session == null)
                return;

            // Do not process day cycle updates while in the lobby.
            if (inLobby)
                return;

            // Retrieve current state markers.
            bool isDayStart = HasSingleton<SIsDayFirstUpdate>();        // Occurs on the first frame of a day.
            bool isPrepTime = HasSingleton<SIsNightTime>();               // Active throughout the prep phase.
            bool isPrepFirstUpdate = HasSingleton<SIsNightFirstUpdate>();   // Active only on the first frame of prep.

            // Arm the cycle on the first day.
            if (!firstCycleCompleted && isDayStart)
            {
                firstCycleCompleted = true;
                dayTransitionProcessed = false;
                itemsSpawnedThisRun = false; // Reset this so items spawn correctly
                Logger.LogInfo("First day cycle completed; day cycle updates are now armed.");
            }

            // Only spawn queued items when we enter the next prep phase after day cycle updates are armed
            if (firstCycleCompleted && isPrepTime)
            {
                while (spawnQueue.Count > 0)
                {
                    ItemInfo queuedInfo = spawnQueue.Dequeue();
                    Logger.LogInfo($"[Next Run Prep] Spawning queued item ID: {(int)queuedInfo.ItemId}");
                    ProcessSpawn(queuedInfo);
                }
            }


            // Process the day-to-night transition only on the first frame of prep.
            if (firstCycleCompleted && isPrepFirstUpdate && !dayTransitionProcessed)
            {
                lastDay++;
                Logger.LogInfo("Transitioning from day to night, advancing day count to: " + lastDay);
                session.Locations.CompleteLocationChecks(dayID + lastDay);
                int presentdayID = dayID + lastDay;
                Logger.LogInfo("Day Logged " + lastDay + " with ID " + presentdayID);
                prepLogDone = false;
                loggedCardThisCycle = false;

                // Process dish-based day checks
                int dishCount = selectedDishes.Count;
                if (dishCount == 0)
                {
                    Logger.LogWarning("[Dish Check] No selected dishes found. Skipping dish-based checks.");
                }
                else
                {
                    for (int i = 0; i < dishCount; i++)
                    {
                        string dishName = selectedDishes[i];

                        // Process check only for the currently selected dish
                        if (ProgressionMapping.dishDictionary.TryGetValue(DishId, out string selectedDishName))
                        {
                            if (ProgressionMapping.dish_id_lookup.TryGetValue(selectedDishName, out int activeDishID))
                            {
                                int activeDishDayCheckID = (activeDishID * 1000) + lastDay;
                                session.Locations.CompleteLocationChecks(activeDishDayCheckID);
                                Logger.LogInfo($"[Dish Check] Completed location check for selected dish {selectedDishName} on Day {lastDay} with ID {activeDishDayCheckID}");
                            }
                            else
                            {
                                Logger.LogWarning($"[Dish Check] Selected dish '{selectedDishName}' not found in lookup table.");
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"[Dish Check] Dish ID {DishId} not found in dishDictionary.");
                        }
                    }

                    // Award stars every three days.
                    if (lastDay % 3 == 0 && lastDay <= 15) 
                    {
                        stars++;
                        int starCheckID = (dayID + lastDay) * 10 + 1;

                        Logger.LogInfo($"[Star Check] Awarding Star {stars} with ID: {starCheckID}");
                        session.Locations.CompleteLocationChecks(starCheckID);

                        // Special handling for 4th and 5th stars to ensure they're sent in franchises
                        if (stars == 4)
                        {
                            int star4CheckID = (dayID + 12) * 10 + 1;
                            Logger.LogInfo($"[Star Check] Sending missing Star 4 check with ID: {star4CheckID}");
                            session.Locations.CompleteLocationChecks(star4CheckID);
                        }
                        if (stars == 5)
                        {
                            int star5CheckID = (dayID + 15) * 10 + 1;
                            Logger.LogInfo($"[Star Check] Sending missing Star 5 check with ID: {star5CheckID}");
                            session.Locations.CompleteLocationChecks(star5CheckID);

                            Logger.LogInfo("[Star Check] Star 5 achieved. Resetting star counter.");
                            stars = 0; // Reset after reaching 5 stars
                        }
                    }
                }

                dayTransitionProcessed = true;
            }
            else if (!isPrepFirstUpdate)
            {
                dayTransitionProcessed = false;
            }
        }

        private void ApplySpeedModifiers()
        {
            using (var playerEntities = playerSpeedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < playerEntities.Length; i++)
                {
                    Entity playerEntity = playerEntities[i];
                    // Get the player's component (assumes CPlayer contains a Speed field)
                    CPlayer player = EntityManager.GetComponentData<CPlayer>(playerEntity);
                    // Apply the movement speed modifier from the current tier.
                    player.Speed = player.Speed * movementSpeedMod;
                    EntityManager.SetComponentData(playerEntity, player);
                }
            }
        }

        private void ApplyApplianceSpeedModifiers()
        {
            if (!HasSingleton<SIsDayTime>())
                return;

            float speedMultiplier = applianceSpeedMod;

            EntityQuery processingApplianceQuery = GetEntityQuery(ComponentType.ReadOnly<CItemUndergoingProcess>());
            var processingAppliances = processingApplianceQuery.ToEntityArray(Allocator.TempJob);

            var entityManager = EntityManager;
            for (int i = 0; i < processingAppliances.Length; i++)
            {
                Entity itemEntity = processingAppliances[i];

                if (!entityManager.HasComponent<CItemUndergoingProcess>(itemEntity))
                    continue;

                var itemProcess = entityManager.GetComponentData<CItemUndergoingProcess>(itemEntity);
                Entity applianceEntity = itemProcess.Appliance;

                // Check if appliance has a speed modifier component
                if (!entityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                    continue;

                var speedMod = entityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                float originalSpeed = speedMod.Speed;

                // 🛠️ FIX: Set the speed directly instead of multiplying
                speedMod.Speed = speedMultiplier;
                entityManager.SetComponentData(applianceEntity, speedMod);

                Mod.Logger.LogInfo($"[ApplyApplianceSpeedModifiers] Set appliance {applianceEntity.Index} speed to {speedMultiplier}, previous speed {originalSpeed}");
            }

            processingAppliances.Dispose();
        }



        public void IncreaseApplianceSpeedTier()
        {
            if (applianceSpeedTier < applianceSpeedTiers.Length - 1)
            {
                applianceSpeedTier++;
                applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];

                Debug.Log($"[Mod] Appliance speed upgraded to tier {applianceSpeedTier}, new speed multiplier = {applianceSpeedMod}");
            }
            else
            {
                Debug.LogWarning("[Mod] Appliance speed is already at maximum tier.");
            }
        }

        public static Dictionary<int, float> originalApplianceSpeeds = new Dictionary<int, float>();

        public static float GetSpeedMultiplier(Appliance appliance)
        {
            int tierIndex = Mathf.Clamp(applianceSpeedTier, 0, applianceSpeedTiers.Length - 1);
            float multiplier = applianceSpeedTiers[tierIndex];

            return Mathf.Clamp(multiplier, 0.1f, 2f);
        }

        private void TrySubscribeItemsSync()
        {
            if (session == null)
            {
                Logger.LogError("Session is null, cannot subscribe to items.");
                return;
            }

            while (session.Items == null)
            {
                Logger.LogInfo("Waiting for session items to initialize...");
                System.Threading.Thread.Sleep(1000);
            }

            if (!itemsEventSubscribed)
            {
                session.Items.ItemReceived += new ReceivedItemsHelper.ItemReceivedHandler(OnItemReceived);
                itemsEventSubscribed = true;
                Logger.LogInfo("Subscribed to session.Items.ItemReceived.");
            }
        }
        private void SendGoalComplete()
        {
            Logger.LogInfo("Sending final completion to Archipelago!");
            var statusUpdate = new StatusUpdatePacket();
            statusUpdate.Status = ArchipelagoClientState.ClientGoal;
            session.Socket.SendPacket(statusUpdate);
        }
    }
}
