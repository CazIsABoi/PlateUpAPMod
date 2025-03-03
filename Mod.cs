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
using KitchenDecorOnDemand;
using PreferenceSystem;
using PreferenceSystem.Event;
using PreferenceSystem.Menus;
using KitchenPlateupAP;

namespace KitchenPlateupAP
{
    [HarmonyPatch(typeof(Archipelago.MultiClient.Net.Converters.ArchipelagoPacketConverter))]
    [HarmonyPatch("ReadJson")]
    public class Patch_ArchipelagoPacketConverter_ReadJson
    {
        static void Postfix(object __result)
        {
            if (__result is List<string> stringList)
            {
                stringList.RemoveAll(s => s == "Disabled");
            }
        }
    }
    //Player Speed Patch
    [HarmonyPatch(typeof(DeterminePlayerSpeed), "OnUpdate")]
    public static class Patch_DeterminePlayerSpeed_OnUpdate
    {
        static void Postfix(DeterminePlayerSpeed __instance)
        {
            // Only apply the multiplier if the SIsDayTime marker exists.
            if (!__instance.HasSingleton<SIsDayTime>())
                return;

            EntityQuery query = __instance.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<CPlayer>());
            using (var playerEntities = query.ToEntityArray(Allocator.Temp))
            {
                var em = __instance.EntityManager;
                for (int i = 0; i < playerEntities.Length; i++)
                {
                    Entity playerEntity = playerEntities[i];
                    CPlayer player = em.GetComponentData<CPlayer>(playerEntity);
                    // Multiply the player's speed by your mod multiplier.
                    player.Speed *= Mod.movementSpeedMod;
                    em.SetComponentData(playerEntity, player);
                }
            }
        }
    }


    public class ArchipelagoConfig
    {
        public string address { get; set; }
        public int port { get; set; }
        public string playername { get; set; }
        public string password { get; set; }
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
        public const string MOD_VERSION = "0.1.5";
        public const string MOD_AUTHOR = "Caz";
        public const string MOD_GAMEVERSION = ">=1.1.9";

        internal static AssetBundle Bundle = null;
        internal static KitchenLib.Logging.KitchenLogger Logger;
        private EntityQuery playersWithItems;
        private EntityQuery playerSpeedQuery;

        public static Mod Instance { get; private set; }

        private static ArchipelagoSession session => ArchipelagoConnectionManager.Session;
        private static int chosenGoal = 1;  // default to "Franchise Twice" if missing
        private static List<string> selectedDishes = new List<string>();
        object rawGoal = null;
        private static bool dishesMessageSent = false;
        private bool itemsQueuedThisLobby = false;

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
        private static readonly float[] speedTiers = new float[] { 0.5f, 0.75f, 1.0f, 1.1f, 1.15f };
        private static int movementSpeedTier = 0;
        private static int interactionSpeedTier = 0;
        private static int cookingSpeedTier = 0;

        // Set initial multipliers from the tiers:
        public static float movementSpeedMod = speedTiers[movementSpeedTier];
        private static float interactionSpeedMod = speedTiers[interactionSpeedTier];
        private static float cookingSpeedMod = speedTiers[cookingSpeedTier];

        public static class InputSourceIdentifier
        {
            public static int Identifier = 0;
        }

        // Appliance Dictionary
        private readonly Dictionary<int, int> progressionToGDO = new Dictionary<int, int>()
        {
            { 1001, ApplianceReferences.Hob },
            { 10012, ApplianceReferences.HobSafe },
            { 10013, ApplianceReferences.HobDanger },
            { 10014, ApplianceReferences.HobStarting },
            { 10015, ApplianceReferences.Oven },
            { 10016, ApplianceReferences.Microwave },
            { 10017, ApplianceReferences.GasLimiter },
            { 10018, ApplianceReferences.GasSafetyOverride },
            { 1002, ApplianceReferences.SinkNormal },
            { 10022, ApplianceReferences.SinkPower },
            { 10023, ApplianceReferences.SinkSoak },
            { 10024, ApplianceReferences.SinkStarting },
            { 10025, ApplianceReferences.DishWasher },
            { 10026, ApplianceReferences.SinkLarge },
            { 1003, ApplianceReferences.Countertop },
            { 10032, ApplianceReferences.Workstation },
            { 10033, ApplianceReferences.Freezer },
            { 10034, ApplianceReferences.PrepStation },
            { 10035, ApplianceReferences.FrozenPrepStation },
            { 1004, ApplianceReferences.TableLarge },
            { 10042, ApplianceReferences.TableBar },
            { 10043, ApplianceReferences.TableBasicCloth },
            { 10044, ApplianceReferences.TableCheapMetal },
            { 10045, ApplianceReferences.TableFancyCloth },
            { 10046, ApplianceReferences.CoffeeTable },
            { 1005, ApplianceReferences.BinStarting },
            { 10052, ApplianceReferences.Bin },
            { 10053, ApplianceReferences.BinCompactor },
            { 10054, ApplianceReferences.BinComposter },
            { 10055, ApplianceReferences.BinExpanded },
            { 10056, ApplianceReferences.FloorProtector },
            { 1006, ApplianceReferences.RollingPinProvider },
            { 10062, ApplianceReferences.SharpKnifeProvider },
            { 10063, ApplianceReferences.ScrubbingBrushProvider },
            { 1007, ApplianceReferences.BreadstickBox },
            { 10072, ApplianceReferences.CandleBox },
            { 10073, ApplianceReferences.NapkinBox },
            { 10074, ApplianceReferences.SharpCutlery },
            { 10075, ApplianceReferences.SpecialsMenuBox },
            { 10076, ApplianceReferences.LeftoversBagStation },
            { 10077, ApplianceReferences.SupplyCabinet },
            { 10078, ApplianceReferences.HostStand },
            { 10079, ApplianceReferences.FlowerPot },
            { 1008, ApplianceReferences.MopBucket },
            { 10082, ApplianceReferences.MopBucketLasting },
            { 10083, ApplianceReferences.MopBucketFast },
            { 10084, ApplianceReferences.RobotMop },
            { 10085, ApplianceReferences.FloorBufferStation },
            { 10086, ApplianceReferences.RobotBuffer },
            { 10087, ApplianceReferences.DirtyPlateStack },
            { 1009, ApplianceReferences.Belt },
            { 10092, ApplianceReferences.Grabber },
            { 10093, ApplianceReferences.GrabberSmart },
            { 10094, ApplianceReferences.GrabberRotatable },
            { 10095, ApplianceReferences.Combiner },
            { 10096, ApplianceReferences.Portioner },
            { 10097, ApplianceReferences.Mixer },
            { 10098, ApplianceReferences.MixerPusher },
            { 10099, ApplianceReferences.MixerHeated },
            { 100992, ApplianceReferences.MixerRapid },
            { 1011, ApplianceReferences.BlueprintCabinet },
            { 10112, ApplianceReferences.BlueprintUpgradeDesk },
            { 10113, ApplianceReferences.BlueprintOrderingDesk },
            { 10114, ApplianceReferences.BlueprintDiscountDesk },
            { 10115, ApplianceReferences.ClipboardStand },
            { 10116, ApplianceReferences.BlueprintCopyDesk },
            { 1012, ApplianceReferences.ShoeRackTrainers },
            { 10122, ApplianceReferences.ShoeRackWellies },
            { 10123, ApplianceReferences.ShoeRackWorkBoots },
            { 1013, ApplianceReferences.BookingDesk },
            { 10132, ApplianceReferences.FoodDisplayStand },
            { 10133, ApplianceReferences.Dumbwaiter },
            { 10134, ApplianceReferences.Teleporter },
            { 10135, ApplianceReferences.FireExtinguisherHolder },
            { 10136, ApplianceReferences.OrderingTerminal },
            { 10137, ApplianceReferences.OrderingTerminalSpecialOffers },
            { 1014, ApplianceReferences.PlateStackStarting },
            { 10142, ApplianceReferences.PlateStack },
            { 10143, ApplianceReferences.AutoPlater },
            { 10144, ApplianceReferences.PotStack },
            { 10145, ApplianceReferences.ServingBoardStack },
            { 1015, ApplianceReferences.CoffeeMachine },
            { 10152, ApplianceReferences.IceDispenser },
            { 10153, ApplianceReferences.MilkDispenser },
            { 10154, ApplianceReferences.WokStack },
            { 10155, ApplianceReferences.SourceLasagneTray },
            { 10156, ApplianceReferences.ProviderTacoTray },
            { 10157, ApplianceReferences.ProviderMixingBowls },
            { 10158, ApplianceReferences.SourceBigCakeTin },
            { 10159, ApplianceReferences.SourceBrownieTray },
            { 1016, ApplianceReferences.SourceCookieTray },
            { 10162, ApplianceReferences.SourceCupcakeTray },
            { 10163, ApplianceReferences.SourceDoughnutTray },
            { 10164, ApplianceReferences.ExtraLife }
        };

        private readonly Dictionary<int, string> dishDictionary = new Dictionary<int, string>()
        {           
            { DishReferences.SaladBase, "Salad Base" },
            { DishReferences.SteakBase, "Steak" },
            { DishReferences.BurgerBase, "Burger" },
            { DishReferences.CoffeeBaseDessert, "Coffee" },
            { DishReferences.PizzaBase, "Pizza" },
            { DishReferences.Dumplings, "Dumplings" },
            { DishReferences.TurkeyBase, "Turkey" },
            { DishReferences.PieBase, "Pie" },
            { DishReferences.Cakes, "Cakes" },
            { DishReferences.SpaghettiBolognese, "Spaghetti" },
            { DishReferences.FishBase, "Fish" },
            { DishReferences.TacosBase, "Tacos" },
            { DishReferences.HotdogBase, "Hot Dogs" },
            { DishReferences.BreakfastBase, "Breakfast" },
            { DishReferences.StirFryBase, "Stir Fry" },
        };

        //ID comparison
        private readonly Dictionary<string, int> dish_id_lookup = new Dictionary<string, int>
        {
            { "Salad", 101 },
            { "Steak", 102 },
            { "Burger", 103 },
            { "Coffee", 104 },
            { "Pizza", 105 },
            { "Dumplings", 106 },
            { "Turkey", 107 },
            { "Pie", 108 },
            { "Cakes", 109 },
            { "Spaghetti", 110 },
            { "Fish", 111 },
            { "Tacos", 112 },
            { "Hot Dogs", 113 },
            { "Breakfast", 114 },
            { "Stir Fry", 115 }
        };

        private static readonly Dictionary<int, string> speedUpgradeMapping = new Dictionary<int, string>()
        {
            { 10, "Speed Upgrade Player" },
        };


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
                timesFranchised++;
                dayID = 100000 * timesFranchised;
                session.Locations.CompleteLocationChecks(dayID);
                franchised = true;

                Logger.LogInfo($"User has franchised {timesFranchised} time(s). Checking vs chosenGoal={chosenGoal}...");
                if (timesFranchised >= (chosenGoal + 1))
                {
                    SendGoalComplete();
                }
            }

            else if (loseScreen && !lost)
            {
                Logger.LogInfo("You Lost the Run!");
                HandleGameReset();
                session.Locations.CompleteLocationChecks(100000);
                lost = true;

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

                // Only queue if we haven't already done so in this lobby session
                if (spawnQueue.Count == 0)
                {
                    QueueItemsFromReceivedPool(5);
                    Logger.LogInfo($"[Lobby] {spawnQueue.Count} items queued for next run.");
                }
                else
                {
                    Logger.LogInfo("[Lobby] Items are already queued. Skipping queueing.");
                }

                itemsQueuedThisLobby = true; // Prevent multiple calls
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

                                if (dishDictionary.TryGetValue(DishId, out string dishName) && !loggedCardThisCycle)
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

        private static List<int> receivedItemPool = new List<int>(); // Store received item IDs

        private void OnItemReceived(ReceivedItemsHelper helper)
        {
            ItemInfo info = helper.DequeueItem();
            int checkId = (int)info.ItemId;
            Logger.LogInfo($"[OnItemReceived] Received check ID: {checkId}");

            // Store item in the pool (excluding speed upgrades)
            if (!speedUpgradeMapping.ContainsKey(checkId))
            {
                receivedItemPool.Add(checkId);
                Logger.LogInfo($"[OnItemReceived] Item ID {checkId} added to receivedItemPool.");
            }
            else
            {
                Logger.LogInfo($"[OnItemReceived] Item ID {checkId} is a speed upgrade and was not stored.");
            }

            // Process items normally
            // Always queue item first
            Logger.LogInfo($"[OnItemReceived] Queuing item ID: {checkId}");

            // If currently in prep phase, spawn immediately
            bool currentlyPrep = HasSingleton<SIsNightTime>();
            if (currentlyPrep)
            {
                Logger.LogInfo($"[OnItemReceived] Currently in prep phase, queueing item ID: {checkId} for immediate spawn.");
                spawnQueue.Enqueue(info);  // Instead of direct spawn, queue it so UpdateDayCycle handles it
            }

            else
            {
                // If not in prep phase, queue it for the next run
                spawnQueue.Enqueue(info);
                Logger.LogInfo($"[OnItemReceived] Item ID {checkId} is queued for the next run.");
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


        private void HandleGameReset()
        {
            lastDay = 0;
            prepLogDone = false;
            loggedCardThisCycle = false;
            firstCycleCompleted = false;
            dayTransitionProcessed = false;
            itemsSpawnedThisRun = false;

            Logger.LogInfo("[HandleGameReset] Reset complete. Selecting 5 random received items from inventory.");
            QueueItemsFromReceivedPool(5);

            if (spawnQueue.Count > 0)
            {
                Logger.LogInfo($"[HandleGameReset] {spawnQueue.Count} items successfully queued for next run.");
            }
            else
            {
                Logger.LogWarning("[HandleGameReset] No items were queued! Check received items pool.");
            }
        }

        private void ResetToLastStar()
        {
            if (stars > 0)
            {
                stars--;
                lastDay -= 3; // Roll back 3 days per star
                Logger.LogInfo($"[DeathLink] Rolling back to last awarded star. New day: {lastDay}, Stars: {stars}");
            }
            else
            {
                Logger.LogInfo("[DeathLink] No stars to roll back to. Resetting entire run.");
                HandleGameReset();
            }
        }

        private void QueueItemsFromReceivedPool(int count)
            {
                if (session == null || session.Items == null)
                {
                    Logger.LogError("Session or session items are null. Cannot retrieve received items.");
                    return;
                }

                // Log all received items from Archipelago
                Logger.LogInfo($"[QueueItemsFromReceivedPool] Total received items count: {session.Items.AllItemsReceived.Count}");

                if (session.Items.AllItemsReceived.Count == 0)
                {
                    Logger.LogWarning("[QueueItemsFromReceivedPool] No items have been received in this session.");
                    return;
                }

                // Get all received items and use itemID directly
                var receivedItems = session.Items.AllItemsReceived
                    .Select(item => (int)item.ItemId) // Use itemID directly
                    .Where(id => !speedUpgradeMapping.ContainsKey(id)) // Exclude speed upgrades
                    .ToList();

                // Log filtered items
                Logger.LogInfo($"[QueueItemsFromReceivedPool] Non-speed item count: {receivedItems.Count}");

                if (receivedItems.Count == 0)
                {
                    Logger.LogWarning("[QueueItemsFromReceivedPool] No valid non-speed items available to queue for next run.");
                    return;
                }

                // Randomly select 'count' items (or all if less than count)
                var random = new System.Random();
                var selectedItems = receivedItems.OrderBy(_ => random.Next()).Take(count).ToList();

                foreach (int itemId in selectedItems)
                {
                    Logger.LogInfo($"[QueueItemsFromReceivedPool] Queuing item ID {itemId} for next run.");
                    spawnQueue.Enqueue(CreateItemInfoForQueue(itemId));
                }

                Logger.LogInfo($"[QueueItemsFromReceivedPool] {selectedItems.Count} items added to spawn queue.");
            }



        private void QueueItemsForNextRun(int count)
        {
            if (receivedItemPool.Count == 0)
            {
                Logger.LogWarning("No items available to queue for next run.");
                return;
            }

            // Filter out speed upgrades
            var validItems = receivedItemPool.Where(id => !speedUpgradeMapping.ContainsKey(id)).ToList();

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
                Logger.LogInfo($"Queuing item ID {itemId} for next run.");

                // Use the helper function to create ItemInfo
                spawnQueue.Enqueue(CreateItemInfoForQueue(itemId));
            }
        }




        private void ProcessSpawn(ItemInfo info)
        {
            int checkId = (int)info.ItemId;

            // Check if this check is a speed upgrade.
            if (speedUpgradeMapping.ContainsKey(checkId))
            {
                // Increment the tier progressively regardless of which id was received.
                if (movementSpeedTier < speedTiers.Length - 1)
                {
                    movementSpeedTier++;
                    movementSpeedMod = speedTiers[movementSpeedTier];
                    Logger.LogInfo($"{speedUpgradeMapping[checkId]} applied. Movement speed now at tier {movementSpeedTier} (multiplier = {movementSpeedMod}).");
                }
                return;
            }

            // Otherwise, process the check normally (spawn objects, etc.)
            if (progressionToGDO.TryGetValue(checkId, out int gdoId))
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
                        if (dishDictionary.TryGetValue(DishId, out string selectedDishName))
                        {
                            if (dish_id_lookup.TryGetValue(selectedDishName, out int activeDishID))
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
                    if (lastDay % 3 == 0 && lastDay < 15)
                    {
                        stars++;
                        int starCheckID = (dayID + lastDay) * 10 + 1; // Correct calculation

                        Logger.LogInfo($"[Star Check] Awarding Star {stars} with ID: {starCheckID}");
                        session.Locations.CompleteLocationChecks(starCheckID);

                        if (stars == 5)
                        {
                            Logger.LogInfo("[Star Check] Star 5 achieved. Resetting star counter.");
                            stars = 0; // Reset after reaching 5 stars
                        }
                    }
                }


                // Mark that this transition has been processed.
                dayTransitionProcessed = true;
            }
            else if (!isPrepFirstUpdate)
            {
                // Once we leave the first frame of prep, reset the flag for the next cycle.
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
            // (Add similar logic for interaction and cooking if you have corresponding components.)
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
