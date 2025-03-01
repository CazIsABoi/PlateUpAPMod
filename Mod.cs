﻿using System;
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
using KitchenDecorOnDemand;
using PreferenceSystem;
using PreferenceSystem.Event;
using PreferenceSystem.Menus;
using PlateupAP;

namespace PlateupAP
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
        public const string MOD_VERSION = "0.1.4.2";
        public const string MOD_AUTHOR = "Caz";
        public const string MOD_GAMEVERSION = ">=1.1.9";

        internal static AssetBundle Bundle = null;
        internal static KitchenLib.Logging.KitchenLogger Logger;
        private EntityQuery playersWithItems;

        public static Mod Instance { get; private set; }

        private static ArchipelagoSession session => ArchipelagoConnectionManager.Session;

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


        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly())
        {
            Instance = this;
            Logger = InitLogger();
            Logger.LogWarning("Created instance");
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
        }

        public void UpdateArchipelagoConfig(ArchipelagoConfig config)
        {
            // Use static connection manager.
            ArchipelagoConnectionManager.TryConnect(config.address, config.port, config.playername, config.password);
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
                timesFranchised++;
                dayID = 100000 * timesFranchised;
                session.Locations.CompleteLocationChecks(dayID);
                lastDay = 0;
                prepLogDone = false;
                loggedCardThisCycle = false;
                firstCycleCompleted = false;
                dayTransitionProcessed = false;
                franchised = true;
            }

            else if (loseScreen && !lost)
            {
                Logger.LogInfo("You Lost the Run!");
                lastDay = 0;
                session.Locations.CompleteLocationChecks(100000);
                prepLogDone = false;
                loggedCardThisCycle = false;
                firstCycleCompleted = false;
                dayTransitionProcessed = false;
                lost = true;
            }

            else if (inLobby)
            {
                firstCycleCompleted = false;
                previousWasDay = false;
                franchised = false;
                lost = false;
            }


            // --- Dish Card Reading Logic ---
            if (!loggedCardThisCycle)
            {
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
                            int dishID = dishChoice.Dish;
                            DishId = dishID; // Save the dish id for later use.

                            // Retrieve the Dish game data object.
                            Dish dishData = (Dish)GDOUtils.GetExistingGDO(dishID);
                            Logger.LogInfo($"The selected dish is: {dishData.Name}");

                            if (dishDictionary.TryGetValue(dishID, out string dishName))
                            {
                                Logger.LogInfo($"The selected dish (via dictionary) is: {dishName}");
                            }
                            else
                            {
                                Logger.LogInfo($"Dish with ID {dishID} not found in dictionary; using game data: {dishData.Name}");
                            }
                            loggedCardThisCycle = true;
                            break;
                        }

                    }
                }
            }
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

        private void OnItemReceived(ReceivedItemsHelper helper)
        {
            ItemInfo info = helper.DequeueItem();
            int checkId = (int)info.ItemId;
            Logger.LogInfo("Received check id: " + checkId);

            // Use SIsNightTime for item processing so items are sent throughout the prep phase.
            bool currentlyPrep = HasSingleton<SIsNightTime>();
            if (!currentlyPrep)
            {
                Logger.LogInfo("Not in prep phase, queueing check id: " + checkId);
                spawnQueue.Enqueue(info);
                return;
            }
            ProcessSpawn(info);
        }


        private void ProcessSpawn(ItemInfo info)
        {
            int checkId = (int)info.ItemId;
            if (progressionToGDO.TryGetValue(checkId, out int gdoId))
            {
                SpawnPositionType positionType = SpawnPositionType.Door;
                SpawnApplianceMode spawnApplianceMode = SpawnApplianceMode.Blueprint;
                if (KitchenData.GameData.Main.TryGet<Appliance>(gdoId, out Appliance appliance))
                {
                    SpawnRequestSystem.Request<Appliance>(gdoId, positionType, InputSourceIdentifier.Identifier, spawnApplianceMode);
                }
                else if (KitchenData.GameData.Main.TryGet<Decor>(gdoId, out Decor decor))
                {
                    SpawnRequestSystem.Request<Decor>(gdoId, positionType);
                }
                else
                {
                    Logger.LogWarning("GDO id " + gdoId + " does not correspond to a known Appliance or Decor.");
                }
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
                dayTransitionProcessed = false; // Ready for the upcoming transition.
                Logger.LogInfo("First day cycle completed; day cycle updates are now armed.");
            }

            // Flush any queued items if we're in prep (using the SIsNightTime marker).
            if (isPrepTime)
            {
                while (spawnQueue.Count > 0)
                {
                    ItemInfo queuedInfo = spawnQueue.Dequeue();
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
                Logger.LogInfo($"Current saved dish id is: {DishId}");

                // Award stars every three days.
                if (lastDay % 3 == 0 && lastDay < 15)
                {
                    stars++;
                    switch (stars)
                    {
                        case 1:
                            session.Locations.CompleteLocationChecks(presentdayID * 10 + 1);
                            Logger.LogInfo("Star 1 gotten, Logged " + presentdayID);
                            break;
                        case 2:
                            session.Locations.CompleteLocationChecks(presentdayID * 10 + 1);
                            Logger.LogInfo("Star 2 gotten, Logged " + presentdayID);
                            break;
                        case 3:
                            session.Locations.CompleteLocationChecks(presentdayID * 10 + 1);
                            Logger.LogInfo("Star 3 gotten, Logged " + presentdayID);
                            break;
                        case 4:
                            session.Locations.CompleteLocationChecks(presentdayID * 10 + 1);
                            Logger.LogInfo("Star 4 gotten, Logged " + presentdayID);
                            break;
                        case 5:
                            session.Locations.CompleteLocationChecks(presentdayID * 10 + 1);
                            Logger.LogInfo("Star 5 gotten, Logged " + presentdayID + " Resetting star counter");
                            stars = 0;
                            break;
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
    }
}
