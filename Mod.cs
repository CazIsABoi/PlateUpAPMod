using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using HarmonyLib;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.Logging;
using KitchenLib.References;
using KitchenLib.Utils;
using KitchenMods;
using KitchenPlateupAP.Spawning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PreferenceSystem;
using PreferenceSystem.Event;
using PreferenceSystem.Menus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KitchenPlateupAP
{
    public class PlateupAPConfig
    {
        [JsonProperty] public string address { get; set; }
        [JsonProperty] public int port { get; set; }
        [JsonProperty] public string playername { get; set; }
        [JsonProperty] public string password { get; set; }

    }

    public partial class Mod : BaseMod, IModSystem
    {
        public const string MOD_GUID = "com.caz.plateupap";
        public const string MOD_NAME = "PlateupAP";
        public const string MOD_VERSION = "0.2.5.4";    
        public const string MOD_AUTHOR = "Caz";
        public const string MOD_GAMEVERSION = ">=1.1.9";
        public static int TOTAL_SCENES_LOADED = 0;

        // Minimal addition: track whether upgrades have been randomized this boot
        private static bool upgradesRandomized = false;

        internal static AssetBundle Bundle = null;
        internal static KitchenLib.Logging.KitchenLogger Logger;
        private EntityQuery playersWithItems;
        private EntityQuery playerSpeedQuery;
        private EntityQuery applianceSpeedQuery;
        private EntityQuery progressionUnlockQuery;
        private EntityQuery settingQuery;
        private static RunIdentity currentIdentity;
        private static PendingSpawnState pendingSpawnState = new PendingSpawnState();
        private static bool persistenceLoaded = false;

        public static Mod Instance { get; private set; }
        internal static PlateupAPConfig CachedConfig;
        private static bool _configWarmed;

        private static ArchipelagoSession session => ArchipelagoConnectionManager.Session;
        private Archipelago.MultiClient.Net.BounceFeatures.DeathLink.DeathLinkService deathLinkService;
        private int deathLinkBehavior = 0; // Default to "Reset Run"
        private bool suppressNextDeathLink = false;
        private static int goal = 0;             // 0 = franchise_x_times, 1 = complete_x_days, 2 = reach_day_x_with_dishes
        private static int franchiseCount = 0;   // how many times to franchise
        private static int dayCount = 1;        // how many days to complete
        private static int dayTarget = 15;       // goal 2: global day the player must survive to (15–30)
        private static int dishGoalCount = 3;    // goal 2: number of dishes that must be active on that day
        private static List<string> selectedDishes = new List<string>();
        private static bool dishesMessageSent = false;
        private bool itemsQueuedThisLobby = false;
        int itemsKeptPerRun = 5;
        public static int RandomTrapCardCount = 0;
        bool deathLinkResetToLastStarPending = false;
        public static int applianceSpeedMode = 0;
        private static bool checksDisabled = false;
        private bool dishPedestalSpawned = false;
        private static int dayLeaseInterval = 5;
        public static int MoneyCap = 10;
        private static int baseMoneyCap = 20;
        private const int MoneyCapIncrementStep = 10;
        private bool wasInLobbyLastFrame = false;
        private string lastAppliedStartingName = string.Empty;
        private bool startingNameApplied = false;
        public static int ExtraBlueprintCount = 0;
        int startingCardsMode = 0;
        int startingCardsAmount = 0;
        int removeCardCount = 0;

        // Counts from slot data (defaults per spec = 5)
        private static int playerSpeedUpgradeCount = 5;
        private static int applianceSpeedUpgradeCount = 5;

        // Static day cycle and spawn state.
        private static int lastDay = 0;
        private int dayID = 100000;
        private int stars = 0;
        private int timesFranchised = 0; // number of completed franchises so far
        private int DishId;
        private bool firstCycleCompleted = false;
        bool inLobby = true;
        bool loseScreen = false;
        bool franchiseScreen = false;
        bool lost = false;
        bool franchised = false;
        private bool dayTransitionProcessed = false;
        private static int overallDaysCompleted = 0;
        private static int overallStarsEarned = 0;
        public static int TotalLeaseItemsReceived = 0;
        private static bool itemsEventSubscribed = false;
        private static Queue<ItemInfo> spawnQueue = new Queue<ItemInfo>();
        private bool franchisePending = false;
        private bool moneyClampedThisPrep = false;
        private bool forceSpawnRequested = false;
        private bool moneyClampPending = false;
        private static int pendingCoinAmount = 0;

        // Flag to prevent repeated logging during a cycle.
        private static bool prepLogDone = false;
        private static bool sessionNotInitLogged = false;
        private bool itemsSpawnedThisRun = false;
        private static Dictionary<int, float> playerBaseSpeeds = new Dictionary<int, float>();
        private int currentDishDayCount = 0;
        private int dishIdTrackedForDayCount = 0;
        private int lastCardSyncDishId = 0;

        // Build tiers dynamically from slot data: start 0.5, + (1.0 / N) per upgrade, final 1.5 at N upgrades; if N==0 -> [1.0]
        private static float[] speedTiers = BuildPlayerSpeedTiers(playerSpeedUpgradeCount);
        private static int movementSpeedTier = 0;

        //Modifying Appliance Values
        public static readonly float[] applianceSpeedTiers = { -0.25f, -0.15f, 0f, 0.1f, 0.2f };
        public static int applianceSpeedTier = 0;
        public static readonly float[] chopSpeedTiers = { -0.25f, -0.15f, 0f, 0.1f, 0.2f };
        public static int chopSpeedTier = 0;
        public static readonly float[] cleanSpeedTiers = { -0.25f, -0.15f, 0f, 0.1f, 0.2f };
        public static int cleanSpeedTier = 0;
        public static readonly float[] cookSpeedTiers = { -0.25f, -0.15f, 0f, 0.1f, 0.2f };
        public static int cookSpeedTier = 0;


        // Set initial multipliers from the tiers:
        public static float movementSpeedMod = speedTiers[0];
        public static float applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];
        public static float chopSpeedMod = chopSpeedTiers[chopSpeedTier];
        public static float cookSpeedMod = cookSpeedTiers[cookSpeedTier];
        public static float cleanSpeedMod = cleanSpeedTiers[cleanSpeedTier];

        // Appliance shop locking
        public static bool ApplianceUnlocksEnabled = false;
        private static HashSet<int> _unlockedApplianceGDOs = new HashSet<int>();

        public static bool IsApplianceUnlocked(int gdoId) => !ApplianceUnlocksEnabled || _unlockedApplianceGDOs.Contains(gdoId);

        public static void UnlockAppliance(int gdoId)
        {
            _unlockedApplianceGDOs.Add(gdoId);
            Logger?.LogInfo($"[ApplianceUnlocks] Unlocked GDO {gdoId}. Total unlocked: {_unlockedApplianceGDOs.Count}");
        }

        // Decoration unlocks
        public static bool DecorationUnlocksEnabled = false;
        private static HashSet<int> _unlockedDecorationGDOs = new HashSet<int>();
        public static bool IsDecorationUnlocked(int gdoId) => !DecorationUnlocksEnabled || _unlockedDecorationGDOs.Contains(gdoId);
        public static void UnlockDecoration(int gdoId)
        {
            _unlockedDecorationGDOs.Add(gdoId);
            Logger?.LogInfo($"[DecorationUnlocks] Unlocked decoration GDO {gdoId}. Total unlocked: {_unlockedDecorationGDOs.Count}");
        }

        public static class InputSourceIdentifier
        {
            public static int Identifier = 0;
        }

        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly())
        {
            Instance = this;
            Logger = InitLogger();
            Logger.LogWarning("Created instance");
            // Replace inline lambda with method to also warm config
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Compute dynamic tiers from slot data count
        private static float[] BuildPlayerSpeedTiers(int count)
        {
            if (count <= 0)
                return new[] { 1f };
            var tiers = new float[count + 1];
            float step = 1f / count; // 100% / count as multiplier
            for (int i = 0; i <= count; i++)
            {
                tiers[i] = 0.5f + (i * step);
            }
            return tiers;
        }

        // Add near the top of Mod class
        private const string CustomAppliancesFileName = "custom_appliances.json";
        private const string CustomAppliancesReadmeName = "custom_appliances.readme.txt";

        private static string GetConfigFolderPath()
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.GetFullPath(Path.Combine(appData, "..", "LocalLow", "It's Happening", "PlateUp", "PlateUpAPConfig"));
                return folder;
            }

            // Fallback for macOS/Linux
            return Path.Combine(Application.persistentDataPath, "PlateUpAPConfig");
        }

        private static string GetCustomAppliancesFilePath()
        {
            return Path.Combine(GetConfigFolderPath(), CustomAppliancesFileName);
        }

        private static string GetCustomAppliancesReadmePath()
        {
            return Path.Combine(GetConfigFolderPath(), CustomAppliancesReadmeName);
        }

        private void EnsureCustomAppliancesFileExists()
        {
            try
            {
                string folder = GetConfigFolderPath();
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                // Create JSON file (array of integers: GDO IDs)
                string jsonPath = GetCustomAppliancesFilePath();
                if (!File.Exists(jsonPath))
                {
                    File.WriteAllText(jsonPath, "[]");
                    Logger.LogInfo($"[CustomAppliances] Created file at: {jsonPath}");
                }

                // Create README guidance
                string readmePath = GetCustomAppliancesReadmePath();
                if (!File.Exists(readmePath))
                {
                    var guide = string.Join(Environment.NewLine, new[]
                    {
                "Custom Appliances Guide",
                "",
                "Add Appliance GDO IDs to custom_appliances.json as a JSON array. Example:",
                "",
                "[",
                "  10097,  // Mixer",
                "  10112   // Research Desk",
                "]",
                "",
                "How to find GDO IDs:",
                "- I recommend this: https://steamcommunity.com/sharedfiles/filedetails/?id=2933828796",
                "",
                "How to search in the mod:",
                "- Open with CTRL + SHIFT + T",
                "- Go to: GDOs > KitchenData.Appliance",
                "- Search for the custom appliance to confirm its GDO ID",
                "",
                "Notes:",
                "- Invalid or unknown IDs are ignored.",
                "- Both Appliances and Decor are supported.",
                "- This file is for guidance only; edit the JSON file to add IDs."
            });
                    File.WriteAllText(readmePath, guide);
                    Logger.LogInfo($"[CustomAppliances] Created README at: {readmePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[CustomAppliances] Failed to ensure files exist: {ex.Message}");
            }
        }

        // Apply player speed count -> rebuild tiers and clamp current tier; update multiplier
        private static void ApplyPlayerSpeedConfig()
        {
            speedTiers = BuildPlayerSpeedTiers(playerSpeedUpgradeCount);
            movementSpeedTier = Mathf.Clamp(movementSpeedTier, 0, speedTiers.Length - 1);
            movementSpeedMod = speedTiers[movementSpeedTier];
            playerBaseSpeeds.Clear();
            Logger?.LogInfo($"[PlateupAP] Player speed tiers rebuilt for count={playerSpeedUpgradeCount}. Levels={speedTiers.Length}, currentTier={movementSpeedTier}, multiplier={movementSpeedMod}");
        }

        // New: scene callback, warms config on main menu
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogInfo($"{TOTAL_SCENES_LOADED} Loaded Scene: {scene.name}");
            TOTAL_SCENES_LOADED++;

            // Main menu scene appears to be "Scene" from your logs
            if (!_configWarmed && string.Equals(scene.name, "Scene", StringComparison.OrdinalIgnoreCase))
            {
                _configWarmed = true;
                TryWarmupConfig();
            }
        }

        private static RunIdentity BuildIdentity()
        {
            if (CachedConfig == null)
                return null;
            return new RunIdentity
            {
                Address = CachedConfig.address ?? "",
                Port = CachedConfig.port,
                Player = CachedConfig.playername ?? ""
            };
        }

        public void UpdateArchipelagoConfig(PlateupAPConfig config)
        {
            CachedConfig = config;
            currentIdentity = BuildIdentity();
            if (currentIdentity != null)
            {
                bool reset = PersistenceManager.ShouldResetForIdentity(currentIdentity);
                if (reset)
                {
                    Logger.LogInfo("[Persistence] Identity changed (port/address/player). Resetting stored speed upgrades and pending items.");
                    PersistenceManager.ResetForNewRun(currentIdentity);
                }
                PersistenceManager.SaveIdentity(currentIdentity);
            }
            ArchipelagoConnectionManager.TryConnect(config.address, config.port, config.playername, config.password);
        }

        private void TryWarmupConfig()
        {
            try
            {
                // Ensure the custom file exists early (main menu)
                EnsureCustomAppliancesFileExists();

                string folder = Path.Combine(Application.persistentDataPath, "PlateUpAPConfig");
                string path = Path.Combine(folder, "archipelago_config.json");
                if (!File.Exists(path))
                {
                    Logger.LogWarning($"[PlateupAP][ConfigWarmup] No config at: {path}");
                    return;
                }

                var json = File.ReadAllText(path);
                // Use isolated manual parse to avoid converter interference
                var jo = JObject.Parse(json);
                var cfg = new PlateupAPConfig
                {
                    address = (string)jo["address"],
                    port = (int?)jo["port"] ?? 0,
                    playername = (string)jo["playername"],
                    password = (string)jo["password"]
                };

                if (string.IsNullOrWhiteSpace(cfg.address))
                {
                    Logger.LogWarning("[PlateupAP][ConfigWarmup] Address is empty in config; will not cache.");
                    return;
                }

                if (cfg.port <= 0 || string.IsNullOrWhiteSpace(cfg.playername))
                {
                    Logger.LogWarning("[PlateupAP][ConfigWarmup] Config incomplete (missing port or player name); cached but skipping auto-connect.");
                    CachedConfig = cfg;
                    return;
                }

                CachedConfig = cfg;
                Logger.LogInfo($"[PlateupAP][Config] Using server={cfg.address}:{cfg.port} player={cfg.playername}");
                Logger.LogInfo("[PlateupAP][Config] Auto-connecting...");
                UpdateArchipelagoConfig(cfg);
            }
            catch (Exception ex)
            {
                Logger.LogError("[PlateupAP][ConfigWarmup] Failed: " + ex.Message);
            }
        }
        private void RetrieveSlotData()
        {
            if (session == null)
                return; // Not connected

            var slotData = ArchipelagoConnectionManager.SlotData;

            if (slotData != null)
            {
                Logger.LogInfo($"[PlateupAP] Full Slot Data: {JsonConvert.SerializeObject(slotData, Formatting.Indented)}");

                if (ArchipelagoConnectionManager.SlotData.TryGetValue("starting_cards", out object rawStartingCards))
                    int.TryParse(rawStartingCards.ToString(), out startingCardsMode);

                if (ArchipelagoConnectionManager.SlotData.TryGetValue("starting_cards_amount", out object rawStartingAmount))
                    int.TryParse(rawStartingAmount.ToString(), out startingCardsAmount);

                // Use SlotIndex as a deterministic seed so removal order is stable across reconnects
                StartingCardManager.Initialise(startingCardsMode, startingCardsAmount, ArchipelagoConnectionManager.SlotIndex);

                // In RetrieveSlotData(), replace the selected_dishes handling so ONLY the first dish (or starting_dish) is unlocked as baseline
                if (slotData.TryGetValue("selected_dishes", out object rawDishes))
                {
                    Logger.LogInfo($"[PlateupAP] Found selected_dishes in slot data: {rawDishes}");
                    try
                    {
                        selectedDishes = JsonConvert.DeserializeObject<List<string>>(rawDishes.ToString()) ?? new List<string>();

                        // Optional explicit starting dish override
                        string startingDishName = null;
                        if (slotData.TryGetValue("starting_dish", out object rawStartingDish))
                        {
                            startingDishName = rawStartingDish?.ToString();
                        }

                        // Resolve all provided dish names to GDO IDs (for logging/persisting)
                        var resolvedDishIds = selectedDishes
                            .Select(name => ProgressionMapping.dishDictionary
                                .FirstOrDefault(kv => string.Equals(kv.Value, name, StringComparison.OrdinalIgnoreCase)).Key)
                            .Where(id => id != 0)
                            .Distinct()
                            .ToList();

                        // Determine baseline dish: starting_dish if valid, else first of selected list
                        int baselineDishId = 0;
                        if (!string.IsNullOrWhiteSpace(startingDishName))
                        {
                            baselineDishId = ProgressionMapping.dishDictionary
                                .FirstOrDefault(kv => string.Equals(kv.Value, startingDishName, StringComparison.OrdinalIgnoreCase)).Key;
                        }
                        if (baselineDishId == 0 && resolvedDishIds.Count > 0)
                        {
                            baselineDishId = resolvedDishIds[0];
                        }

                        if (baselineDishId != 0)
                        {
                            // Baseline only; other dishes stay locked until unlocked by items
                            LockedDishes.SetUnlockedDishes(new[] { baselineDishId });
                            LockedDishes.EnableLocking();

                            PersistLastSelectedDishes(selectedDishes);
                            Logger.LogInfo($"[PlateupAP] Baseline dish unlocked: {baselineDishId} (selected list: {string.Join(", ", resolvedDishIds)})");
                        }
                        else
                        {
                            LockedDishes.DisableLocking();
                            Logger.LogWarning("[PlateupAP] Could not resolve any baseline dishes. Locking disabled.");
                        }
                    }
                    catch (JsonReaderException ex)
                    {
                        LockedDishes.DisableLocking();
                        Logger.LogError($"[PlateupAP] Error parsing selected_dishes JSON: {ex.Message}. Locking disabled.");
                    }
                }

                if (slotData.TryGetValue("goal", out object rawGoal))
                {
                    goal = Convert.ToInt32(rawGoal);
                    Logger.LogInfo($"[PlateupAP] Goal set to: {goal} (0=franchise_x_times, 1=complete_x_days, 2=reach_day_x_with_dishes)");
                }

                if (slotData.TryGetValue("franchise_count", out object rawFranchiseCount))
                {
                    franchiseCount = Convert.ToInt32(rawFranchiseCount);
                    Logger.LogInfo($"[PlateupAP] Franchise count goal: {franchiseCount}");
                }

                if (slotData.TryGetValue("day_count", out object rawDayCount))
                {
                    dayCount = Convert.ToInt32(rawDayCount);
                    Logger.LogInfo($"[PlateupAP] Day count goal: {dayCount}");
                }

                if (slotData.TryGetValue("day_target", out object rawDayTarget))
                {
                    dayTarget = Mathf.Clamp(Convert.ToInt32(rawDayTarget), 15, 30);
                    Logger.LogInfo($"[PlateupAP] Day target (goal 2): {dayTarget}");
                }

                if (slotData.TryGetValue("dish_goal_count", out object rawDishGoalCount))
                {
                    dishGoalCount = Mathf.Clamp(Convert.ToInt32(rawDishGoalCount), 1, 17);
                    Logger.LogInfo($"[PlateupAP] Dish goal count (goal 2): {dishGoalCount}");
                }

                if (slotData.TryGetValue("death_link", out object rawDeathLink))
                {
                    bool deathLinkEnabled = Convert.ToBoolean(rawDeathLink);
                    Logger.LogInfo($"[PlateupAP] DeathLink enabled: {deathLinkEnabled}");

                    if (deathLinkEnabled)
                    {
                        EnableDeathLink();
                    }
                }

                if (slotData.TryGetValue("death_link_behavior", out object rawBehavior))
                {
                    deathLinkBehavior = Convert.ToInt32(rawBehavior);
                    Logger.LogInfo($"[PlateupAP] DeathLink Behavior Set To: {deathLinkBehavior}");
                }

                if (slotData.TryGetValue("items_kept", out object rawItemsKept))
                {
                    itemsKeptPerRun = Convert.ToInt32(rawItemsKept);
                    Logger.LogInfo($"[PlateupAP] Items Kept Per Run: {itemsKeptPerRun}");
                }

                if (slotData.TryGetValue("appliance_speed_mode", out object rawApplianceSpeedMode))
                {
                    applianceSpeedMode = Convert.ToInt32(rawApplianceSpeedMode);
                    Logger.LogInfo($"[PlateupAP] Appliance Speed Mode set to {applianceSpeedMode} (0=grouped, 1=separate)");
                }

                if (slotData.TryGetValue("day_lease_interval", out object rawLeaseInterval))
                {
                    dayLeaseInterval = Mathf.Clamp(Convert.ToInt32(rawLeaseInterval), 1, 30);
                    Logger.LogInfo($"[PlateupAP] Day Lease Interval set to: {dayLeaseInterval}");
                    KitchenPlateupAP.LeaseRequirementSystem.TriggerRefresh();
                }

                if (slotData.TryGetValue("player_speed_upgrade_count", out object rawPlayerSpeedCount))
                {
                    int value = Mathf.Clamp(Convert.ToInt32(rawPlayerSpeedCount), 0, 10);
                    playerSpeedUpgradeCount = value;
                    Logger.LogInfo($"[PlateupAP] Player Speed Upgrade Count: {playerSpeedUpgradeCount}");
                    ApplyPlayerSpeedConfig();
                }
                else
                {
                    ApplyPlayerSpeedConfig();
                }

                if (slotData.TryGetValue("appliance_speed_upgrade_count", out object rawApplianceSpeedCount))
                {
                    applianceSpeedUpgradeCount = Mathf.Clamp(Convert.ToInt32(rawApplianceSpeedCount), 0, 10);
                    Logger.LogInfo($"[PlateupAP] Appliance Speed Upgrade Count: {applianceSpeedUpgradeCount}");
                }

                if (slotData.TryGetValue("starting_money_cap", out object rawStartingCap))
                {
                    int startingCap = Mathf.Clamp(Convert.ToInt32(rawStartingCap), 0, 999);
                    baseMoneyCap = startingCap;
                    MoneyCap = startingCap;
                    Logger.LogInfo($"[MoneyCap] Starting money cap from slot data set to {MoneyCap}");
                }

                if (slotData.TryGetValue("appliance_unlocks", out object rawApplianceUnlocks))
                {
                    int applianceUnlocksValue = Convert.ToInt32(rawApplianceUnlocks);
                    ApplianceUnlocksEnabled = applianceUnlocksValue == 1;
                    Logger.LogInfo($"[PlateupAP] Appliance Unlocks: {(ApplianceUnlocksEnabled ? "ENABLED" : "DISABLED")}");
                }
                else
                {
                    ApplianceUnlocksEnabled = false;
                }
                if (slotData.TryGetValue("decoration_unlocks", out object rawDecorationUnlocks))
                {
                    int decorUnlocksValue = Convert.ToInt32(rawDecorationUnlocks);
                    DecorationUnlocksEnabled = decorUnlocksValue == 1;
                    Logger.LogInfo($"[PlateupAP] Decoration Unlocks: {(DecorationUnlocksEnabled ? "ENABLED" : "DISABLED")}");
                }
                else
                {
                    DecorationUnlocksEnabled = false;
                }
            }

            if (selectedDishes.Count == 0)
            {
                Logger.LogWarning("[PlateupAP] selectedDishes is empty, no dish to unlock.");
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
            ChatManager.AddSystemMessage("Selected Dishes: " + string.Join(", ", selectedDishes));
        }

        static PreferenceSystemManager PrefManager;

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            try
            {
                if (World == null)
                    Logger.LogError("World is null in OnPostActivate!");

                if (PrefManager == null)
                    PrefManager = new PreferenceSystemManager(MOD_GUID, MOD_NAME);

                if (ArchipelagoConnectionManager.ConnectionSuccessful)
                {
                    RetrieveSlotData();
                    ProcessAllReceivedItems();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[PlateupAP] Error in OnPostActivate: {ex.Message}\n{ex.StackTrace}");
            }

            PrefManager = new PreferenceSystemManager(MOD_GUID, MOD_NAME);
            PrefManager
                .AddLabel("Archipelago Configuration")
                .AddInfo("Create or load configuration for the Archipelago connection")
                .AddInfo(@"Config is found in \AppData\LocalLow\It's Happening\PlateUp")
                .AddButton("Create Config", (int _) =>
                {
                    // Explicitly target LocalLow path: %appdata%\..\LocalLow\It's Happening\PlateUp\PlateUpAPConfig
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %APPDATA%
                    string folder = Path.GetFullPath(Path.Combine(appData, "..", "LocalLow", "It's Happening", "PlateUp", "PlateUpAPConfig"));

                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    string path = Path.Combine(folder, "archipelago_config.json");
                    PlateupAPConfig defaultConfig = new PlateupAPConfig
                    {
                        address = "archipelago.gg",
                        port = 0,
                        playername = "",
                        password = ""
                    };
                    string json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
                    File.WriteAllText(path, json);
                    Logger.LogInfo("Created config file at: " + path);

                    try
                    {
                        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "explorer.exe",
                                Arguments = $"/select,\"{path}\"",
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                        else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "open",
                                Arguments = $"-R \"{path}\"",
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                        else
                        {
                            // Generic fallback: open the containing folder
                            var psi = new ProcessStartInfo
                            {
                                FileName = folder,
                                UseShellExecute = true
                            };
                            System.Diagnostics.Process.Start(psi);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"Could not open file explorer for path '{path}': {ex.Message}");
                    }
                })
                .AddButton("Connect", (int _) =>
                {
                    // Always re-read the config file so edits take effect without restarting
                    string folder = Path.Combine(Application.persistentDataPath, "PlateUpAPConfig");
                    string path = Path.Combine(folder, "archipelago_config.json");
                    if (!File.Exists(path))
                    {
                        Logger.LogError("Config file not found at: " + path);
                        return;
                    }

                    PlateupAPConfig config;
                    string json = File.ReadAllText(path);
                    try
                    {
                        var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
                        config = new PlateupAPConfig
                        {
                            address = (string)jo["address"],
                            port = (int?)jo["port"] ?? 0,
                            playername = (string)jo["playername"],
                            password = (string)jo["password"]
                        };
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[PlateupAP][Config] Manual parse failed: " + ex);
                        Logger.LogError("JSON: " + json);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(config.address))
                    {
                        Logger.LogError("[PlateupAP][Config] Invalid address.");
                        return;
                    }

                    Logger.LogInfo($"[PlateupAP][Config] Using server={config.address}:{config.port} player={config.playername}");
                    UpdateArchipelagoConfig(config);
                })
                // NEW: Debug utilities
                .AddLabel("Debug Utilities")
                .AddInfo("Quick fixes during a run")
                .AddButton("Set Player Speed to 1x", (int _) => { ForcePlayerSpeedToOne(); })
                .AddButton("Increment Franchise Count", (int _) => { IncrementFranchiseAndCheckGoal(); })
                .AddButton("Spawn Queued Items Now", (int _) =>
                {
                    forceSpawnRequested = true;
                    Logger.LogInfo("[Debug] Spawn Queued Items requested; will process in OnUpdate.");
                })
                .AddButton("Send All Received Checks", (int _) =>
                {
                    SendAllReceivedChecks();
                })
                .AddButton("Increase Money Cap by 10", (int _) =>
                {
                    int before = MoneyCap;
                    MoneyCap = Mathf.Clamp(MoneyCap + 10, 0, 9999);
                    Logger.LogInfo($"[MoneyCap] Cap increased from {before} to {MoneyCap}");
                })
                .AddButton("Uncap Money Cap", (int _) =>
                {
                    MoneyCap = 9999;
                    Logger.LogInfo("[MoneyCap] Cap set to 9999");
                })
                .AddButton("Unlock All Dishes", (int _) =>
                {
                    var allDishIds = ProgressionMapping.dishDictionary.Keys.ToList();
                    LockedDishes.AddUnlockedDishes(allDishIds);
                    LockedDishes.EnableLocking();

                    foreach (int dishId in allDishIds)
                    {
                        PersistUnlockedDish(dishId);
                    }

                    Logger.LogWarning($"[Debug] Unlocked all {allDishIds.Count} dishes: {string.Join(", ", allDishIds.Select(id => ProgressionMapping.dishDictionary[id]))}");
                    ChatManager.AddSystemMessage($"All {allDishIds.Count} dishes unlocked.");
                })
  .AddButton("Create/Open Custom Appliances", (int _) =>
  {
      try
      {
          EnsureCustomAppliancesFileExists();
          string folder = GetConfigFolderPath();
          string jsonPath = GetCustomAppliancesFilePath();
          string readmePath = GetCustomAppliancesReadmePath();

          if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
          {
              // Open folder
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = "explorer.exe",
                  Arguments = $"\"{folder}\"",
                  UseShellExecute = true
              });
              // Open files via shell association
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = jsonPath,
                  UseShellExecute = true
              });
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = readmePath,
                  UseShellExecute = true
              });
          }
          else if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
          {
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = "open",
                  Arguments = $"\"{folder}\"",
                  UseShellExecute = true
              });
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = "open",
                  Arguments = $"\"{jsonPath}\"",
                  UseShellExecute = true
              });
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = "open",
                  Arguments = $"\"{readmePath}\"",
                  UseShellExecute = true
              });
          }
          else
          {
              // Fallback: open folder only
              System.Diagnostics.Process.Start(new ProcessStartInfo
              {
                  FileName = folder,
                  UseShellExecute = true
              });
          }

          Logger.LogInfo($"[CustomAppliances] Opened: {folder}");
      }
      catch (Exception ex)
      {
          Logger.LogWarning("[CustomAppliances] Could not open files: " + ex.Message);
      }
  });

            PrefManager.RegisterMenu(PreferenceSystemManager.MenuType.MainMenu);
            PrefManager.RegisterMenu(PreferenceSystemManager.MenuType.PauseMenu);

            if (GameObject.FindObjectOfType<ChatManager>() == null)
            {
                var obj = new GameObject("ChatManager");
                obj.AddComponent<ChatManager>();
                UnityEngine.Object.DontDestroyOnLoad(obj);
            }

            ChatManager.AddSystemMessage("PlateUp Archipelago loaded.");
        }

        // Call EnsureCustomAppliancesFileExists in startup paths
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
            progressionUnlockQuery = GetEntityQuery(new QueryHelper().All(typeof(CProgressionUnlock)));
            ApplyPlayerSpeedConfig();
            World.GetOrCreateSystem<MoneyCapSystem>().Enabled = true;
            EnsureCustomAppliancesFileExists();
            settingQuery = GetEntityQuery(ComponentType.ReadOnly<CSetting>());
            ArchipelagoConnectionManager.Disconnected += (_) => { itemsEventSubscribed = false; };
        }

        public void OnSuccessfulConnect()
        {
            if (ArchipelagoConnectionManager.ConnectionSuccessful)
            {
                EnsureItemsSubscription(); // subscribe early so lobby packets are handled
                 upgradesRandomized = false;
                 TryRandomizeUpgradesOnce();
                 RetrieveSlotData(); // Fetch slot data
                 EnsureDishLockingBaseline(); // <<< ensure we have a baseline to lock against

                // Load persistence once per connection (before applying past items)
                if (!persistenceLoaded)
                {
                    currentIdentity = BuildIdentity();
                    if (currentIdentity != null)
                    {
                        var speedState = PersistenceManager.LoadSpeedState(currentIdentity);
                        if (speedState != null)
                        {
                            movementSpeedTier = Mathf.Clamp(speedState.MovementTier, 0, speedTiers.Length - 1);
                            applianceSpeedTier = Mathf.Clamp(speedState.ApplianceTier, 0, applianceSpeedTiers.Length - 1);
                            cookSpeedTier = Mathf.Clamp(speedState.CookTier, 0, cookSpeedTiers.Length - 1);
                            chopSpeedTier = Mathf.Clamp(speedState.ChopTier, 0, chopSpeedTiers.Length - 1);
                            cleanSpeedTier = Mathf.Clamp(speedState.CleanTier, 0, cleanSpeedTiers.Length - 1);

                            movementSpeedMod = speedTiers[movementSpeedTier];
                            applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];
                            cookSpeedMod = cookSpeedTiers[cookSpeedTier];
                            chopSpeedMod = chopSpeedTiers[chopSpeedTier];
                            cleanSpeedMod = cleanSpeedTiers[cleanSpeedTier];

                            Logger.LogInfo($"[Persistence] Loaded speed tiers: M={movementSpeedTier} A={applianceSpeedTier} Cook={cookSpeedTier} Chop={chopSpeedTier} Clean={cleanSpeedTier}");
                        }
                        else
                        {
                            Logger.LogInfo("[Persistence] No prior speed state file found for this identity.");
                        }

                        pendingSpawnState = PersistenceManager.LoadPendingSpawn(currentIdentity) ?? new PendingSpawnState();
                        if (pendingSpawnState.PendingItemIDs.Count > 0)
                        {
                            Logger.LogInfo($"[Persistence] Restored {pendingSpawnState.PendingItemIDs.Count} pending items to spawn queue.");
                            var dishUnlockIds = new HashSet<int>(ProgressionMapping.dishUnlockIDs.Values);
                            foreach (int id in pendingSpawnState.PendingItemIDs.ToList())
                            {
                                if (id == 15 || id == 16 || id == 22 || id == 100 || dishUnlockIds.Contains(id))
                                {
                                    pendingSpawnState.PendingItemIDs.Remove(id);
                                    continue;
                                }
                                if (!spawnQueue.Any(x => (int)x.ItemId == id))
                                    spawnQueue.Enqueue(CreateItemInfoForQueue(id));
                            }
                        }
                     }
                     persistenceLoaded = true;
                 }

                // Re-apply upgrades from session history (will clamp; persistence prevents over-increment)
                Logger.LogInfo("[Archipelago] Re-processing all previously received items...");
                ProcessAllReceivedItems();
                ReapplyMoneyCapFromHistory();
                Logger.LogInfo("[Archipelago] Re-processing all previously received location checks");
                ReconstructProgressFromLocationChecks();
                EnsureDishLockingBaseline(); // <<< re-check after item history

                if (World != null)
                {

                    // Reinitialize systems based on appliance speed mode
                    if (applianceSpeedMode == 0)
                    {
                        World.GetOrCreateSystem<ApplyApplianceSpeedModifierSystem>().Enabled = true;
                        World.GetOrCreateSystem<UpdateSeparateApplianceSpeedModifiersSystem>().Enabled = false;
                        World.GetOrCreateSystem<ApplyCleanSpeedSystem>().Enabled = false;
                        World.GetOrCreateSystem<ApplyChopSpeedSystem>().Enabled = false;
                        World.GetOrCreateSystem<ApplyCookSpeedSystem>().Enabled = false;
                        World.GetOrCreateSystem<ApplyKneadSpeedSystem>().Enabled = false;
                        Logger.LogInfo("[OnSuccessfulConnect] Grouped mode enabled, separate-mode disabled.");
                    }
                    else
                    {
                        World.GetOrCreateSystem<ApplyApplianceSpeedModifierSystem>().Enabled = false;
                        World.GetOrCreateSystem<UpdateSeparateApplianceSpeedModifiersSystem>().Enabled = true;
                        World.GetOrCreateSystem<ApplyCleanSpeedSystem>().Enabled = true;
                        World.GetOrCreateSystem<ApplyChopSpeedSystem>().Enabled = true;
                        World.GetOrCreateSystem<ApplyCookSpeedSystem>().Enabled = true;
                        World.GetOrCreateSystem<ApplyKneadSpeedSystem>().Enabled = true;
                        Logger.LogInfo("[OnSuccessfulConnect] Separate mode enabled, grouped mode disabled.");
                    }
                }

                if (!dishesMessageSent && LockedDishes.GetAvailableDishes().Any())
                {
                    SendSelectedDishesMessage();
                    dishesMessageSent = true;
                    Logger.LogInfo("Selected dishes message sent successfully.");
                }
                else if (!LockedDishes.GetAvailableDishes().Any())
                {
                    Logger.LogWarning("No unlocked dishes available.");
                }

                // OPTIONAL: sanitize any string permission collections that contain "Disabled"
                try
                {
                    var ci = ArchipelagoConnectionManager.Session?.ConnectionInfo;
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning("[PlateupAP] Post-login permission cleanup failed: " + ex.Message);
                }

                // OPTIONAL: sanitize any string permission collections that contain "Disabled"
                try
                {
                    var ci = ArchipelagoConnectionManager.Session?.ConnectionInfo;
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning("[PlateupAP] Post-login permission cleanup failed: " + ex.Message);
                }
            }
        }

        // Helper: compute base offset for a run (runIndex: 0 = initial run, 1 = after 1 franchise, ...)
        private static int ComputeRunBaseOffset(int runIndex)
        {
            if (runIndex < 10)
                return (runIndex + 1) * 100000;
            return (runIndex + 11) * 100000; // skip dish range after 10
        }

        // Helper: compute the location ID for "Franchise N times" (n: 1..50)
        private static int ComputeFranchiseTimesLocationId(int n)
        {
            if (n <= 10)
                return 100000 * (n + 1);
            return 100000 * (n + 11);
        }

        private void ReconstructProgressFromLocationChecks()
        {
            if (session == null || session.Locations == null)
            {
                Logger.LogError("[Archipelago] Session or Locations is null. Cannot reconstruct progress.");
                return;
            }

            var checkedLocations = session.Locations.AllLocationsChecked;
            Logger.LogInfo("[Reconstruct] All checked locations: " + string.Join(", ", checkedLocations));

            if (goal == 0)
            {
                // Franchise goal: Rebuild timesFranchised from the set of "Franchise N times" checks
                int count = 0;
                for (int i = 1; i <= 50; i++)
                {
                    int id = ComputeFranchiseTimesLocationId(i);
                    if (checkedLocations.Contains(id))
                        count++;
                }
                timesFranchised = count;
                dayID = ComputeRunBaseOffset(timesFranchised);
                Logger.LogInfo($"[Reconstruct] timesFranchised reconstructed as: {timesFranchised}, current run base offset={dayID}");
            }
            else if (goal == 1 || goal == 2)
            {
                // Day goal / Dish goal: Count all valid day and star completions, and find lastDay for dish checks
                overallDaysCompleted = 0;
                overallStarsEarned = 0;
                int lastFranchiseIdx = -1;
                int idx = 0;

                // Find the last franchise index (based on lose run not applicable here)
                foreach (var loc in checkedLocations)
                {
                    // Optional: could mark runs by base offsets too if needed
                    if (loc == 100000)
                        lastFranchiseIdx = idx; // 100000 is Lose Run, just keep order marker
                    idx++;
                }

                // Find lastDay after last franchise, and count days/stars overall
                int tempIdx = 0;
                int tempLastDay = 0;
                foreach (var loc in checkedLocations)
                {
                    if (loc >= 110000 && loc < 120000)
                    {
                        overallDaysCompleted++;
                        if (tempIdx > lastFranchiseIdx)
                        {
                            int dayNum = (int)(loc - 110000);
                            if (dayNum > tempLastDay)
                                tempLastDay = dayNum;
                        }
                    }
                    if (loc >= 120000 && loc < 130000)
                    {
                        overallStarsEarned++;
                    }
                    tempIdx++;
                }
                lastDay = tempLastDay;

                Logger.LogInfo($"[Reconstruct] overallDaysCompleted: {overallDaysCompleted}, overallStarsEarned: {overallStarsEarned}, lastDay (for dish checks): {lastDay}");
            }

            foreach (var item in session.Items.AllItemsReceived)
            {
                if (item.ItemId == 21)
                    removeCardCount++;
            }
            StartingCardManager.SetRemoveCount(removeCardCount);

            // Count total lease items (item ID 15) in inventory (regardless of goal)
            Mod.TotalLeaseItemsReceived = session.Items.AllItemsReceived.Count(item => (int)item.ItemId == 15);
            Logger.LogInfo($"[Reconstruct] Total lease items received: {Mod.TotalLeaseItemsReceived}");
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

            suppressNextDeathLink = true;
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
                suppressNextDeathLink = false;
            }
        }

        private void SendDeathLink()
        {
            if (suppressNextDeathLink)
            {
                Logger.LogInfo("[PlateupAP] DeathLink suppressed to prevent loop.");
                suppressNextDeathLink = false;
                return;
            }

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

            if (!Require(out SDay day))
                return;

            Logger.LogInfo($"[PlateupAP] Current day: {day.Day}, Stars: {stars}");

            if (stars > 0 && day.Day > 1)
            {
                // Compute how many days past the last multiple of 3
                int overshoot = day.Day % 3;
                // If you're exactly on a multiple, overshoot==0 -> go back 3 days
                int rollbackDays = overshoot == 0 ? 3 : overshoot;
                int newDay = day.Day - rollbackDays;
                newDay = Math.Max(newDay, 1);

                Logger.LogInfo($"[PlateupAP] Rolling back to last star: from {day.Day} to {newDay}");

                // ← Create a fresh entity so the Set() goes out over the socket properly
                Entity entity = base.EntityManager.CreateEntity(typeof(SDay), typeof(CGamePauseBlock));
                Set(entity, new SDay
                {
                    Day = newDay
                });

                Logger.LogInfo($"[PlateupAP] Reset to last earned star complete. Previous day: {day.Day}, New day: {newDay}");
                lastDay = newDay;
            }
            else
            {
                Logger.LogWarning("[PlateupAP] No stars earned or already at day 1, doing full reset instead.");
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
            if (slowEffectExpiry.Count > 0)
            {
                float now = UnityEngine.Time.time;
                var expired = new List<Entity>();
                foreach (var kv in slowEffectExpiry)
                {
                    if (now >= kv.Value)
                        expired.Add(kv.Key);
                }
                foreach (var e in expired)
                {
                    slowEffectMultipliers.Remove(e);
                    slowEffectExpiry.Remove(e);
                    Logger.LogInfo("[Trap] Player speed restored (timer expired).");
                }
            }

            franchiseScreen = HasSingleton<SFranchiseBuilderMarker>();
            loseScreen = HasSingleton<SGameOver>();

            bool currentLobbyState = HasSingleton<SFranchiseMarker>();
            if (!currentLobbyState && wasInLobbyLastFrame)
            {
                itemsQueuedThisLobby = false;
            }
            inLobby = currentLobbyState;
            wasInLobbyLastFrame = currentLobbyState;

            if (inLobby)
            {
                if (!itemsQueuedThisLobby)
                {
                    ResetStateForLobbyEntry();
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

                UpdateRestaurantStartingName();
            }
            else
            {
                dishPedestalSpawned = false;
            }

            if (HasSingleton<SKitchenMarker>())
            {
                if (moneyClampPending)
                {
                    ClampMoneyToCap();
                    moneyClampPending = false;
                }
                if (pendingCoinAmount > 0)
                {
                    if (Require(out SMoney money))
                    {
                        int before = money.Amount;
                        money.Amount += pendingCoinAmount;
                        Set(money);
                        Logger.LogInfo($"[Coins] Added {pendingCoinAmount} coins. Money: {before} -> {money.Amount}");
                    }
                    pendingCoinAmount = 0;
                }
                SyncDishFromActiveCards();
                UpdateDayCycle();
                CheckReceivedItems();
            }
            else
            {
                lastCardSyncDishId = 0;
            }

            if (session == null || session.Locations == null)
            {
                return;
            }
            else if (goal == 0 && franchisePending)
            {
                timesFranchised++;
                int franchiseTimesId = ComputeFranchiseTimesLocationId(timesFranchised);
                session.Locations.CompleteLocationChecks(franchiseTimesId);
                dayID = ComputeRunBaseOffset(timesFranchised);
                Logger.LogInfo($"[Franchise Goal] Franchise completion recorded. Total: {timesFranchised}, sent check ID={franchiseTimesId}, next run base={dayID}");

                if (timesFranchised >= franchiseCount && franchiseCount > 0)
                {
                    Logger.LogInfo("Franchise goal reached! Sending goal complete.");
                    SendGoalComplete();
                }

                franchisePending = false;
            }
            else if (loseScreen && !lost)
            {
                Logger.LogInfo("You Lost the Run! Sending loss check (ID 100000)");
                HandleGameReset();
                lastDay = 0;
                session.Locations.CompleteLocationChecks(100000);
                lost = true;

                if (deathLinkService != null)
                {
                    SendDeathLink();
                }
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
            
            sessionNotInitLogged = false;
            EnsureItemsSubscription();
        }

        private void EnsureItemsSubscription()
       {
           if (itemsEventSubscribed)
              return;

            if (session == null || session.Items == null)
                return;

            session.Items.ItemReceived += OnItemReceived;
            itemsEventSubscribed = true;
            Logger.LogInfo("Subscribed to session.Items.ItemReceived (early).");
        }

        private static List<int> receivedItemPool = new List<int>();

        private void OnItemReceived(IReceivedItemsHelper helper)
        {
            ItemInfo info = helper.DequeueItem();
            long itemIdLong = info.ItemId;
            int checkId = (int)itemIdLong;
            long locationId = info.LocationId;

            string itemName = helper.GetItemName(itemIdLong);
            string locationName = session?.Locations?.GetLocationNameFromId(locationId);

            Logger.LogInfo($"[OnItemReceived] Got item '{itemName}' (ID {itemIdLong}) from location '{locationName}' (ID {locationId})");

            // Traps (e.g., Random Customer Card) apply immediately; don't queue
            if (ProgressionMapping.trapDictionary.ContainsKey(checkId))
            {
                ApplyTrapEffect(checkId);
                pendingSpawnState.PendingItemIDs.Remove(checkId);
                return;
            }

            if (TryHandleDishUnlockFromItem(checkId, itemName))
                return;

            if (checkId == 15)
            {
                Logger.LogInfo("[OnItemReceived] Received Day Lease");
                KitchenPlateupAP.LeaseRequirementSystem.TriggerRefresh();
                pendingSpawnState.PendingItemIDs.Remove(checkId);
                return;
            }

            if (checkId == 16)
            {
                int before = MoneyCap;
                MoneyCap = Mathf.Clamp(MoneyCap + MoneyCapIncrementStep, 0, 999);
                Logger.LogInfo($"[MoneyCap] Received 'Money Cap Increase' (ID 16). Cap increased from {before} to {MoneyCap}.");
                moneyClampPending = true;
                pendingSpawnState.PendingItemIDs.Remove(checkId);
                spawnQueue = new Queue<ItemInfo>(spawnQueue.Where(x => (int)x.ItemId != 16));
                return;
            }

            // Coin items: add money directly
            if (checkId == 17 || checkId == 18 || checkId == 19)
            {
                int coinAmount = 0;
                switch (checkId)
                {
                    case 17: coinAmount = 5; break;
                    case 18: coinAmount = 10; break;
                    case 19: coinAmount = 20; break;
                }
                Logger.LogInfo($"[Coins] Received {coinAmount} coins (item ID {checkId}).");
                pendingCoinAmount += coinAmount;
                pendingSpawnState.PendingItemIDs.Remove(checkId);
                return;
            }

            if (checkId == 21)
            {
                StartingCardManager.ApplyRemoveCard();
                Logger.LogInfo($"[Mod] Remove Card received. {StartingCardManager.ActiveCount} starting card(s) remain.");
                return;
            }

            // Shop Size Increase — MOVED BEFORE speed upgrades so it's reachable
            if (checkId == 22)
            {
                ExtraBlueprintCount++;
                Logger.LogInfo($"[ShopSize] Received 'Shop Size Increase'. Extra blueprints: {ExtraBlueprintCount}");
                pendingSpawnState.PendingItemIDs.Remove(checkId);
                return;
            }

            // Speed upgrades (apply immediately, persist tiers)
            if (ProgressionMapping.speedUpgradeMapping.TryGetValue(checkId, out string upgradeName))
            {
                bool changed = false;
                switch (upgradeName)
                {
                    case "Speed Upgrade Player":
                        if (movementSpeedTier < speedTiers.Length - 1)
                        {
                            movementSpeedTier++;
                            movementSpeedMod = speedTiers[movementSpeedTier];
                            changed = true;
                            Logger.LogInfo($"[OnItemReceived] Player speed upgraded to tier {movementSpeedTier}. Multiplier = {movementSpeedMod}");
                        }
                        Logger.LogInfo("[OnItemReceived] Skipping player speed item for next run.");
                        break;

                    case "Speed Upgrade Appliance":
                        if (applianceSpeedTier < applianceSpeedTiers.Length - 1)
                        {
                            applianceSpeedTier++;
                            applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];
                            changed = true;
                            Logger.LogInfo($"[OnItemReceived] Appliance speed upgraded to tier {applianceSpeedTier}. Multiplier = {applianceSpeedMod}");
                        }
                        break;

                    case "Speed Upgrade Cook":
                        if (cookSpeedTier < cookSpeedTiers.Length - 1)
                        {
                            cookSpeedTier++;
                            cookSpeedMod = cookSpeedTiers[cookSpeedTier];
                            changed = true;
                            Logger.LogInfo($"[OnItemReceived] Cook speed upgraded to tier {cookSpeedTier}. Multiplier = {cookSpeedMod}");
                        }
                        break;

                    case "Speed Upgrade Chop":
                        if (chopSpeedTier < chopSpeedTiers.Length - 1)
                        {
                            chopSpeedTier++;
                            chopSpeedMod = chopSpeedTiers[chopSpeedTier];
                            changed = true;
                            Logger.LogInfo($"[OnItemReceived] Chop/Knead speed upgraded to tier {chopSpeedTier}. Multiplier = {chopSpeedMod}");
                        }
                        break;

                    case "Speed Upgrade Clean":
                        if (cleanSpeedTier < cleanSpeedTiers.Length - 1)
                        {
                            cleanSpeedTier++;
                            cleanSpeedMod = cleanSpeedTiers[cleanSpeedTier];
                            changed = true;
                            Logger.LogInfo($"[OnItemReceived] Clean speed upgraded to tier {cleanSpeedTier}. Multiplier = {cleanSpeedMod}");
                        }
                        break;
                }

                if (changed && currentIdentity != null)
                {
                    var state = new SpeedUpgradeState
                    {
                        MovementTier = movementSpeedTier,
                        ApplianceTier = applianceSpeedTier,
                        CookTier = cookSpeedTier,
                        ChopTier = chopSpeedTier,
                        CleanTier = cleanSpeedTier
                    };
                    PersistenceManager.SaveSpeedState(currentIdentity, state);
                }
                playerBaseSpeeds.Clear();
                return;
            }

            if (ApplianceUnlocksEnabled && ProgressionMapping.progressionToGDO.TryGetValue(checkId, out int unlockedGdo))
            {
                UnlockAppliance(unlockedGdo);
            }

            // Handle appliance unlock items (2001-2062 from apworld)
            if (ProgressionMapping.applianceUnlockToGDO.TryGetValue(checkId, out int unlockGdoId))
            {
                if (ApplianceUnlocksEnabled)
                {
                    UnlockAppliance(unlockGdoId);
                }

                // Resolve appliance name for chat
                string applianceName = null;
                if (KitchenData.GameData.Main.TryGet<Appliance>(unlockGdoId, out var applianceGdo))
                {
                    applianceName = applianceGdo.Name ?? unlockGdoId.ToString();
                }
                applianceName = applianceName ?? unlockGdoId.ToString();
                ChatManager.AddSystemMessage($"Appliance received: {applianceName}");

                if (HasSingleton<SKitchenMarker>())
                {
                    Vector3 spawnPos = SpawnHelpers.ResolveSpawnPosition(EntityManager, SpawnPositionType.Door, InputSourceIdentifier.Identifier);
                    if (KitchenData.GameData.Main.TryGet<Appliance>(unlockGdoId, out _))
                    {
                        SpawnHelpers.TrySpawnApplianceBlueprint(EntityManager, unlockGdoId, spawnPos, costMode: 0f);
                        Logger.LogInfo($"[OnItemReceived] Spawned appliance unlock GDO {unlockGdoId} immediately.");
                    }
                }
                else
                {
                    if (!spawnQueue.Any(x => (int)x.ItemId == checkId))
                    {
                        spawnQueue.Enqueue(info);
                        if (currentIdentity != null)
                        {
                            if (!pendingSpawnState.PendingItemIDs.Contains(checkId))
                                pendingSpawnState.PendingItemIDs.Add(checkId);
                            PersistenceManager.SavePendingSpawn(currentIdentity, pendingSpawnState);
                        }
                        Logger.LogInfo($"[OnItemReceived] Queued appliance unlock ID {checkId} for next prep.");
                    }
                }
                return;
            }

            // Random Decoration Unlock
            if (checkId == 100)
            {
                if (DecorationUnlocksEnabled)
                {
                    var pool = new List<int>();
                    foreach (var kv in ProgressionMapping.decorDictionary)
                    {
                        if (!_unlockedDecorationGDOs.Contains(kv.Value))
                            pool.Add(kv.Value);
                    }

                    if (pool.Count > 0)
                    {
                        int chosen = pool[UnityEngine.Random.Range(0, pool.Count)];
                        UnlockDecoration(chosen);
                        string decorName = ProgressionMapping.decorDictionary.FirstOrDefault(kv => kv.Value == chosen).Key ?? chosen.ToString();
                        Logger.LogInfo($"[DecorationUnlock] Unlocked random decoration: '{decorName}' (GDO {chosen})");
                        ChatManager.AddSystemMessage($"Decoration unlocked: {decorName}");
                    }
                    else
                    {
                        Logger.LogInfo("[DecorationUnlock] All decorations already unlocked.");
                        ChatManager.AddSystemMessage("All decorations already unlocked!");
                    }
                }
                pendingSpawnState.PendingItemIDs.Remove(checkId);
                return;
            }

            // Non-speed items -> add to queue and persist
            receivedItemPool.Add(checkId);

            if (!spawnQueue.Any(x => (int)x.ItemId == checkId))
            {
                spawnQueue.Enqueue(info);

                if (currentIdentity != null)
                {
                    if (!pendingSpawnState.PendingItemIDs.Contains(checkId))
                        pendingSpawnState.PendingItemIDs.Add(checkId);
                    PersistenceManager.SavePendingSpawn(currentIdentity, pendingSpawnState);
                }
                Logger.LogInfo($"[OnItemReceived] Queued item ID {checkId} for spawn.");
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

                if (ApplianceUnlocksEnabled && ProgressionMapping.applianceUnlockToGDO.TryGetValue(itemId, out int historyGdo))
                {
                    UnlockAppliance(historyGdo);
                }

                if (itemId == 22)
                {
                    ExtraBlueprintCount++;
                    Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Shop Size Increase. Extra blueprints: {ExtraBlueprintCount}");
                    continue;
                }

                if (itemId == 100 && DecorationUnlocksEnabled)
                {
                    var pool = new List<int>();
                    foreach (var kv in ProgressionMapping.decorDictionary)
                    {
                        if (!_unlockedDecorationGDOs.Contains(kv.Value))
                            pool.Add(kv.Value);
                    }

                    if (pool.Count > 0)
                    {
                        int chosen = pool[UnityEngine.Random.Range(0, pool.Count)];
                        UnlockDecoration(chosen);
                    }
                    continue;
                }

                if (ProgressionMapping.speedUpgradeMapping.TryGetValue(itemId, out string upgradeType))
                {
                    switch (upgradeType)
                    {
                        case "Speed Upgrade Player":
                            if (movementSpeedTier < speedTiers.Length - 1)
                            {
                                movementSpeedTier++;
                                movementSpeedMod = speedTiers[movementSpeedTier];
                                Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Player Speed Upgrade. Tier: {movementSpeedTier} (x{movementSpeedMod})");
                            }
                            break;

                        case "Speed Upgrade Appliance":
                            if (applianceSpeedTier < applianceSpeedTiers.Length - 1)
                            {
                                applianceSpeedTier++;
                                applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];
                                Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Appliance Speed. Tier: {applianceSpeedTier} (x{applianceSpeedMod})");
                            }
                            break;

                        case "Speed Upgrade Cook":
                            if (cookSpeedTier < cookSpeedTiers.Length - 1)
                            {
                                cookSpeedTier++;
                                cookSpeedMod = cookSpeedTiers[cookSpeedTier];
                                Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Cook Speed. Tier: {cookSpeedTier} (x{cookSpeedMod})");
                            }
                            break;

                        case "Speed Upgrade Chop":
                            if (chopSpeedTier < chopSpeedTiers.Length - 1)
                            {
                                chopSpeedTier++;
                                chopSpeedMod = chopSpeedTiers[chopSpeedTier];
                                Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Chop Speed. Tier: {chopSpeedTier} (x{chopSpeedMod})");
                            }
                            break;

                        case "Speed Upgrade Clean":
                            if (cleanSpeedTier < cleanSpeedTiers.Length - 1)
                            {
                                cleanSpeedTier++;
                                cleanSpeedMod = cleanSpeedTiers[cleanSpeedTier];
                                Logger.LogInfo($"[ProcessAllReceivedItems] Re-applied Clean Speed. Tier: {cleanSpeedTier} (x{cleanSpeedMod})");
                            }
                            break;
                    }
                    continue;
                }

                string itemName = session.Items.GetItemName(itemId);
                TryHandleDishUnlockFromItem(itemId, itemName);
            }
        }

        private void ForceSpawnAllQueuedItems()
        {
            if (World == null)
            {
                Logger.LogWarning("[Debug] World not ready. Cannot force spawn.");
                return;
            }

            if (session == null || session.Items == null)
            {
                Logger.LogWarning("[Debug] Session or Items not ready; cannot force spawn.");
                return;
            }

            if (!HasSingleton<SKitchenMarker>())
            {
                Logger.LogWarning("[Debug] Not in kitchen scene; spawning now could misplace items. Aborting.");
                return;
            }

            if (spawnQueue.Count == 0)
            {
                Logger.LogInfo("[Debug] Spawn queue is empty; nothing to spawn.");
                return;
            }

            int count = spawnQueue.Count;
            ItemInfo[] toSpawn = spawnQueue.ToArray();
            spawnQueue.Clear();

            Logger.LogWarning($"[Debug] Forcing spawn of {count} queued item(s)...");
            foreach (var info in toSpawn)
            {
                ProcessSpawn(info);
            }

            if (currentIdentity != null)
            {
                bool changed = false;
                foreach (int id in toSpawn.Select(i => (int)i.ItemId))
                {
                    if (pendingSpawnState.PendingItemIDs.Remove(id))
                        changed = true;
                }
                if (changed)
                    PersistenceManager.SavePendingSpawn(currentIdentity, pendingSpawnState);
            }

            Logger.LogInfo("[Debug] Forced spawn complete.");
        }

        private void SendAllReceivedChecks()
        {
            try
            {
                if (session == null || session.Items == null || session.Locations == null)
                {
                    Logger.LogWarning("[Debug] Session/Items/Locations not ready; cannot send checks.");
                    return;
                }

                var alreadyChecked = new HashSet<long>(session.Locations.AllLocationsChecked.Select(id => (long)id));
                int sent = 0;

                foreach (var item in session.Items.AllItemsReceived)
                {
                    long locId = item.LocationId;
                    if (locId <= 0)
                        continue;

                    if (!alreadyChecked.Contains(locId))
                    {
                        session.Locations.CompleteLocationChecks((int)locId);
                        sent++;
                        Logger.LogInfo($"[Debug] Sent location check for LocationID={locId} (from received item {item.ItemId}).");
                    }
                    else
                    {
                        Logger.LogInfo($"[Debug] LocationID={locId} already checked; skipping.");
                    }
                }

                Logger.LogWarning($"[Debug] Finished sending checks. Total new checks sent: {sent}.");
            }
            catch (Exception ex)
            {
                Logger.LogError("[Debug] Failed to send all received checks: " + ex.Message);
            }
        }
        private void HandleGameReset()
        {
            Logger.LogInfo("[PlateupAP] Handling game reset...");
            ResetStateForLobbyEntry();
            itemsQueuedThisLobby = false;
            itemsSpawnedThisRun = false;
            franchisePending = false;
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
            HashSet<int> dishUnlockIds = new HashSet<int>(ProgressionMapping.dishUnlockIDs.Values);

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
                    !trapIDs.Contains(id) &&
                    id != 15 && id != 16 &&
                    id != 17 && id != 18 && id != 19 &&
                    id != 22 && id != 100 &&
                    !dishUnlockIds.Contains(id)
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

        private void ProcessSpawn(ItemInfo info)
        {
            int checkId = (int)info.ItemId;
            string itemName = session.Items.GetItemName(checkId);
            if (string.IsNullOrEmpty(itemName))
            {
                Logger.LogWarning("[Spawn] Skipping speed upgrade item ID: " + checkId);
                return;
            }

            if (ProgressionMapping.speedUpgradeMapping.ContainsKey(checkId))
            {
                Logger.LogInfo("[Spawn] Skipping speed upgrade (already applied).");
                return;
            }

            int gdoId = 0;
            if (checkId == 1001)
            {
                var pool = ProgressionMapping.usefulApplianceDictionary.Values.ToList();
                if (pool.Count == 0)
                {
                    Logger.LogWarning("[Spawn] usefulApplianceDictionary is empty; skipping.");
                    return;
                }
                gdoId = pool[UnityEngine.Random.Range(0, pool.Count)];
                Logger.LogInfo($"[Spawn] Random Useful Appliance chosen GDO={gdoId}");
            }
            else if (checkId == 1002)
            {
                var pool = ProgressionMapping.fillerApplianceDictionary.Values.ToList();
                if (pool.Count == 0)
                {
                    Logger.LogWarning("[Spawn] fillerApplianceDictionary is empty; skipping.");
                    return;
                }
                gdoId = pool[UnityEngine.Random.Range(0, pool.Count)];
                Logger.LogInfo($"[Spawn] Random Filler Appliance chosen GDO={gdoId}");
            }
            else
            {
                if (!ProgressionMapping.progressionToGDO.TryGetValue(checkId, out gdoId))
                {
                    if (!ProgressionMapping.applianceUnlockToGDO.TryGetValue(checkId, out gdoId))
                    {
                        Logger.LogWarning("No mapping found for check id: " + checkId);
                        return;
                    }
                }
            }

            Vector3 spawnPos = SpawnHelpers.ResolveSpawnPosition(EntityManager, SpawnPositionType.Door, InputSourceIdentifier.Identifier);

            bool spawned = false;
            if (KitchenData.GameData.Main.TryGet<Appliance>(gdoId, out _))
            {
                spawned = SpawnHelpers.TrySpawnApplianceBlueprint(EntityManager, gdoId, spawnPos, costMode: 0f);
            }
            else if (KitchenData.GameData.Main.TryGet<Decor>(gdoId, out _))
            {
                // Decor.Name is not available in this SDK; spawn and log by ID
                spawned = SpawnHelpers.TrySpawnDecor(EntityManager, gdoId, spawnPos);
            }

            if (spawned)
            {
                // Resolve a friendly name for the chat
                string spawnedName = null;
                if (KitchenData.GameData.Main.TryGet<Appliance>(gdoId, out var spawnedAppliance))
                    spawnedName = spawnedAppliance.Name;
                else
                    spawnedName = ProgressionMapping.decorDictionary
                        .FirstOrDefault(kv => kv.Value == gdoId).Key;
                spawnedName = spawnedName ?? $"GDO {gdoId}";

                ChatManager.AddSystemMessage($"Spawned: {spawnedName}");
                Logger.LogInfo($"[Spawn] Spawned item ID {checkId} (GDO {gdoId}) at {spawnPos}.");
                if (currentIdentity != null && pendingSpawnState.PendingItemIDs.Remove(checkId))
                {
                    PersistenceManager.SavePendingSpawn(currentIdentity, pendingSpawnState);
                }
            }
            else
            {
                Logger.LogWarning($"[Spawn] Failed to spawn item ID {checkId} (GDO {gdoId}). Will remain pending.");
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

        private Dictionary<Entity, float> slowEffectExpiry = new Dictionary<Entity, float>();

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
                    if (!slowEffectMultipliers.ContainsKey(player))
                    {
                        slowEffectMultipliers[player] = 0.25f;
                        slowEffectExpiry[player] = UnityEngine.Time.time + 15f;
                        Logger.LogInfo($"[Trap] Player {i} speed reduced for 15 seconds.");
                    }
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

            // Collect all unlock IDs that are already active (selected or applied)
            var activeCardIds = new HashSet<int>();

            EntityQuery selectedQuery = GetEntityQuery(
                ComponentType.ReadOnly<CProgressionOption>(),
                ComponentType.ReadOnly<CProgressionOption.Selected>());
            using (var entities = selectedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.Exists(entities[i]))
                        continue;
                    activeCardIds.Add(EntityManager.GetComponentData<CProgressionOption>(entities[i]).ID);
                }
            }

            EntityQuery unlockQuery = GetEntityQuery(ComponentType.ReadOnly<CProgressionUnlock>());
            using (var entities = unlockQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.Exists(entities[i]))
                        continue;
                    activeCardIds.Add(EntityManager.GetComponentData<CProgressionUnlock>(entities[i]).ID);
                }
            }

            // Filter to cards not already active
            var availableCards = new List<KeyValuePair<int, int>>();
            foreach (var kv in dict)
            {
                if (!activeCardIds.Contains(kv.Value))
                    availableCards.Add(kv);
            }

            if (availableCards.Count == 0)
            {
                Logger.LogWarning("[Trap] All customer cards are already active, skipping spawn.");
                return;
            }

            int randomIndex = UnityEngine.Random.Range(0, availableCards.Count);
            var chosen = availableCards[randomIndex];
            int unlockCardId = chosen.Value;

            Entity entity = EntityManager.CreateEntity();

            EntityManager.AddComponentData(entity, new CProgressionOption
            {
                ID = unlockCardId,
                FromFranchise = false
            });

            //EntityManager.AddComponent<CSkipShowingRecipe>(entity);

            EntityManager.AddComponent<CProgressionOption.Selected>(entity);

            Logger.LogInfo($"[Trap->RandomCard] Spawned random card key={chosen.Key}, unlockID={unlockCardId}");

            // Persist the new card to the trap card state so it survives continue
            if (currentIdentity != null)
            {
                var trapState = PersistenceManager.LoadTrapCards(currentIdentity) ?? new TrapCardState();
                if (!trapState.SpawnedCardGDOs.Contains(unlockCardId))
                {
                    trapState.SpawnedCardGDOs.Add(unlockCardId);
                    PersistenceManager.SaveTrapCards(currentIdentity, trapState);
                    Logger.LogInfo($"[Trap->RandomCard] Persisted card GDO {unlockCardId} to trap card state.");
                }
            }
        }
        private void UpdateDayCycle()
        {
            if (session == null) return;
            if (inLobby) return;

            bool isDayStart = HasSingleton<SIsDayFirstUpdate>();
            bool isPrepTime = HasSingleton<SIsNightTime>();
            bool isPrepFirstUpdate = HasSingleton<SIsNightFirstUpdate>();

            // Reset the clamp flag at day start
            if (!firstCycleCompleted && isDayStart)
            {
                firstCycleCompleted = true;
                dayTransitionProcessed = false;
                itemsSpawnedThisRun = false;
                moneyClampedThisPrep = false;
                Logger.LogInfo("First day cycle completed; day cycle updates are now armed.");
            }

            // During prep: spawn queued items, then clamp only AFTER the first prep update has passed
            if (firstCycleCompleted && isPrepTime && !isPrepFirstUpdate)
            {
                while (spawnQueue.Count > 0)
                {
                    ItemInfo queued = spawnQueue.Dequeue();
                    Logger.LogInfo($"[Prep Phase] Spawning queued item ID: {queued.ItemId}");
                    ProcessSpawn(queued);
                }

                // Spawn extra blueprints from Shop Size Increase items
                if (ExtraBlueprintCount > 0 && !itemsSpawnedThisRun)
                {
                    SpawnExtraBlueprints();
                }

                if (!moneyClampedThisPrep)
                {
                    ClampMoneyToCap();
                    moneyClampedThisPrep = true;
                }
            }
            else if (firstCycleCompleted && isPrepFirstUpdate && !dayTransitionProcessed)
            {
                LogPrepDishSnapshot();
                dayTransitionProcessed = true;

                // Read the actual game day — this is the day that just completed
                int gameDay = 0;
                if (Require(out SDay sDay))
                    gameDay = sDay.Day;

                // Use SDay as the authoritative lastDay for all goals
                lastDay = gameDay;

                if (goal == 0)
                {
                    Logger.LogInfo($"[Franchise Goal] End of Day {lastDay} this run (SDay={gameDay}).");
                    int dayLocationID = dayID + lastDay;
                    session.Locations.CompleteLocationChecks(dayLocationID);
                    Logger.LogInfo($"[Franchise Goal] Completed location check => ID={dayLocationID}");

                    if (lastDay == 15 && !franchisePending)
                    {
                        franchisePending = true;
                        Logger.LogInfo("[Franchise Goal] Franchise completion is now pending.");
                    }

                    if (lastDay <= 15)
                    {
                        DoDishChecks(lastDay);
                        DoSettingChecks(lastDay);
                        if (lastDay % 3 == 0)
                        {
                            stars++;
                            Logger.LogInfo($"[Franchise Goal] Earned star #{stars} on day {lastDay}.");
                            int[] franchiseStarOffsets = { 0, 31, 61, 91, 121, 151 };
                            if (stars <= 5)
                            {
                                int starLocID = dayID + franchiseStarOffsets[stars];
                                session.Locations.CompleteLocationChecks(starLocID);
                                Logger.LogInfo($"[Franchise Goal] Completed star location => ID={starLocID}");
                            }
                            if (stars >= 5)
                                stars = 0;
                        }
                    }
                }
                else if (goal == 1)
                {
                    overallDaysCompleted++;
                    Logger.LogInfo($"[Day Goal] Overall day {overallDaysCompleted} completed (SDay={gameDay}).");
                    if (overallDaysCompleted <= 100)
                    {
                        int dayLocID = 110000 + overallDaysCompleted;
                        session.Locations.CompleteLocationChecks(dayLocID);
                        Logger.LogInfo($"[Day Goal] Completed location => ID={dayLocID}");
                    }
                    if (lastDay <= 15)
                    {
                        DoDishChecks(lastDay);
                        DoSettingChecks(lastDay);
                    }
                    if (overallDaysCompleted % 3 == 0 && overallStarsEarned < 33)
                    {
                        overallStarsEarned++;
                        int starLocID = 120000 + overallStarsEarned;
                        session.Locations.CompleteLocationChecks(starLocID);
                        Logger.LogInfo($"[Day Goal] Earned star #{overallStarsEarned}, location => ID={starLocID}");
                    }
                    if (overallDaysCompleted >= dayCount)
                    {
                        Logger.LogInfo($"[Day Goal] Reached {overallDaysCompleted} >= {dayCount}, sending goal complete.");
                        SendGoalComplete();
                    }
                }
                else if (goal == 2)
                {
                    overallDaysCompleted++;
                    Logger.LogInfo($"[Dish Day Goal] Overall day {overallDaysCompleted} completed (SDay={gameDay}).");

                    if (overallDaysCompleted <= dayTarget)
                    {
                        int dayLocID = 110000 + overallDaysCompleted;
                        session.Locations.CompleteLocationChecks(dayLocID);
                        Logger.LogInfo($"[Dish Day Goal] Completed day location => ID={dayLocID}");
                    }

                    if (lastDay <= dayTarget)
                    {
                        DoDishChecks(lastDay);
                        DoSettingChecks(lastDay);
                    }

                    int maxStars = dayTarget / 3;
                    if (overallDaysCompleted % 3 == 0 && overallStarsEarned < maxStars)
                    {
                        overallStarsEarned++;
                        int starLocID = 120000 + overallStarsEarned;
                        session.Locations.CompleteLocationChecks(starLocID);
                        Logger.LogInfo($"[Dish Day Goal] Earned star #{overallStarsEarned}, location => ID={starLocID}");
                    }

                    if (overallDaysCompleted >= dayTarget)
                    {
                        int activeDishes = CountActiveDishes();
                        Logger.LogInfo($"[Dish Day Goal] Reached day_target={dayTarget}. Active dishes: {activeDishes}, required: {dishGoalCount}");
                        if (activeDishes >= dishGoalCount)
                        {
                            Logger.LogInfo($"[Dish Day Goal] Win condition met! {activeDishes} >= {dishGoalCount} dishes active. Sending goal complete.");
                            SendGoalComplete();
                        }
                        else
                        {
                            Logger.LogWarning($"[Dish Day Goal] Day target reached but only {activeDishes}/{dishGoalCount} dishes active. Goal NOT complete.");
                        }
                    }
                }
            }
            else if (!isPrepFirstUpdate)
            {
                dayTransitionProcessed = false;
            }
        }

        private void DoDishChecks(int dayNumber)
        {
            if (checksDisabled)
                return;

            if (!ProgressionMapping.dishDictionary.TryGetValue(DishId, out string dishName))
            {
                Logger.LogWarning($"[Dish Check] Dish ID {DishId} not found in dictionary.");
                return;
            }

            if (!ProgressionMapping.dish_id_lookup.TryGetValue(dishName, out int dishID))
            {
                Logger.LogWarning($"[Dish Check] Dish name '{dishName}' not found in lookup.");
                return;
            }

            if (dishIdTrackedForDayCount != DishId)
            {
                ResetDishDayCounter(DishId);
            }

            currentDishDayCount++;

            // Guard: don't send a check beyond the actual game day
            int gameDay = 0;
            if (Require(out SDay sDay))
                gameDay = sDay.Day;

            if (gameDay > 0 && currentDishDayCount > gameDay)
            {
                Logger.LogInfo($"[Dish Check] Skipping: dishDay={currentDishDayCount} > SDay={gameDay} for '{dishName}'. Clamping back.");
                currentDishDayCount = gameDay;
                return;
            }

            int dishCheckID = (dishID * 10000) + currentDishDayCount;

            // Skip if already checked (idempotent)
            if (session.Locations.AllLocationsChecked.Contains(dishCheckID))
            {
                Logger.LogInfo($"[Dish Check] Already sent dishDay={currentDishDayCount} for '{dishName}', skipping.");
                return;
            }

            session.Locations.CompleteLocationChecks(dishCheckID);
            Logger.LogInfo($"[Dish Check] RunDay={dayNumber}, dishDay={currentDishDayCount}, dish='{dishName}', ID={dishCheckID}");
        }


        private void DoSettingChecks(int dayNumber)
        {
            if (checksDisabled)
                return;

            if (session == null || !ArchipelagoConnectionManager.ConnectionSuccessful)
                return;

            var slotData = ArchipelagoConnectionManager.SlotData;
            if (slotData == null)
                return;

            List<string> selectedSettings = null;
            if (slotData.TryGetValue("selected_settings", out object rawSettings))
            {
                try
                {
                    selectedSettings = ((JArray)rawSettings).ToObject<List<string>>();
                }
                catch { }
            }

            if (selectedSettings == null || selectedSettings.Count == 0)
                return;

            if (lastDay < 1 || lastDay > 15)
                return;

            if (settingQuery == null || settingQuery.IsEmptyIgnoreFilter)
            {
                Logger.LogWarning("[SettingCheck] settingQuery is null or empty; no CSetting entity found.");
                return;
            }

            using (var entities = settingQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                if (entities.Length == 0)
                {
                    Logger.LogWarning("[SettingCheck] No entities with CSetting component found.");
                    return;
                }

                var cSetting = EntityManager.GetComponentData<CSetting>(entities[0]);
                int settingId = cSetting.RestaurantSetting;

                if (!ProgressionMapping.TryResolveSettingDisplay(settingId, out string displayName))
                {
                    Logger.LogWarning($"[SettingCheck] Unknown setting ID {settingId}, cannot resolve display name.");
                    return;
                }

                // Case-insensitive match against selected_settings from slot data
                bool found = selectedSettings.Any(s => string.Equals(s, displayName, StringComparison.OrdinalIgnoreCase));
                if (!found)
                {
                    Logger.LogInfo($"[SettingCheck] Setting '{displayName}' not in selected_settings [{string.Join(", ", selectedSettings)}], skipping.");
                    return;
                }

                if (!ProgressionMapping.TryComputeSettingLocationId(settingId, lastDay, out int locId))
                {
                    Logger.LogWarning($"[SettingCheck] Could not compute location ID for setting={settingId}, day={lastDay}.");
                    return;
                }

                session.Locations.CompleteLocationChecks(locId);
                Logger.LogInfo($"[SettingCheck] Sent setting check: '{displayName} - Day {lastDay}' (locId={locId})");
            }
        }

        public void IncreaseApplianceSpeedTier()
        {
            if (applianceSpeedTier < applianceSpeedTiers.Length - 1)
            {
                applianceSpeedTier++;
                applianceSpeedMod = applianceSpeedTiers[applianceSpeedTier];

                Logger.LogInfo($"[Mod] Appliance speed upgraded to tier {applianceSpeedTier}, new speed multiplier = {applianceSpeedMod}");
            }
            else
            {
                Logger.LogWarning("[Mod] Appliance speed is already at maximum tier.");
            }
        }

        public static float GetSpeedMultiplier(Appliance appliance)
        {
            int tierIndex = Mathf.Clamp(applianceSpeedTier, 0, applianceSpeedTiers.Length - 1);
            float multiplier = applianceSpeedTiers[tierIndex];

            return Mathf.Clamp(multiplier, 0.1f, 2f);
        }
        private void SendGoalComplete()
        {
            Logger.LogInfo("Sending final completion to Archipelago!");
            var statusUpdate = new StatusUpdatePacket();
            statusUpdate.Status = ArchipelagoClientState.ClientGoal;
            session.Socket.SendPacket(statusUpdate);
        }

        private const string UnlockedDishFile = "unlocked_dish.txt";

        private int? LoadPersistedUnlockedDish()
        {
            string path = Path.Combine(Application.persistentDataPath, UnlockedDishFile);
            if (File.Exists(path) && int.TryParse(File.ReadAllText(path), out int id))
                return id;
            return null;
        }

        private void PersistUnlockedDish(int dishId)
        {
            string path = Path.Combine(Application.persistentDataPath, UnlockedDishFile);
            File.WriteAllText(path, dishId.ToString());
        }

        private const string LastSelectedDishesFile = "last_selected_dishes.txt";

        private void PersistLastSelectedDishes(List<string> dishes)
        {
            string path = Path.Combine(Application.persistentDataPath, LastSelectedDishesFile);
            File.WriteAllLines(path, dishes);
        }

        // Forces the movement speed mod to 1.0 and persists the new tier.
        private void ForcePlayerSpeedToOne()
        {
            // Rebuild tiers if needed so 1.0 exists (it always will: either N==0 -> [1.0], or 0.5..1.5 includes 1.0)
            ApplyPlayerSpeedConfig();
            // Find nearest tier to 1.0
            int closest = 0;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < speedTiers.Length; i++)
            {
                float d = Math.Abs(speedTiers[i] - 1f);
                if (d < bestDiff)
                {
                    bestDiff = d;
                    closest = i;
                }
            }
            movementSpeedTier = closest;
            movementSpeedMod = speedTiers[movementSpeedTier];

            // Clear cached bases to be safe; next ApplySpeedModifiers will re-cache and apply 1x
            playerBaseSpeeds.Clear();

            Logger.LogWarning("[Debug] Forced player movement speed to 1x.");

            if (currentIdentity != null)
            {
                var state = new SpeedUpgradeState
                {
                    MovementTier = movementSpeedTier,
                    ApplianceTier = applianceSpeedTier,
                    CookTier = cookSpeedTier,
                    ChopTier = chopSpeedTier,
                    CleanTier = cleanSpeedTier
                };
                PersistenceManager.SaveSpeedState(currentIdentity, state);
            }
        }

        private int GetHighestDishDayFromChecks(int dishGdoId)
        {
            if (session == null || session.Locations == null)
                return 0;

            if (!ProgressionMapping.dishDictionary.TryGetValue(dishGdoId, out string dishName))
                return 0;

            if (!ProgressionMapping.dish_id_lookup.TryGetValue(dishName, out int dishID))
                return 0;

            int baseId = dishID * 10000;
            int highest = 0;

            foreach (long locId in session.Locations.AllLocationsChecked)
            {
                if (locId > baseId && locId < baseId + 10000)
                {
                    int dayN = (int)(locId - baseId);
                    if (dayN > highest)
                        highest = dayN;
                }
            }

            return highest;
        }

        private void ResetDishDayCounter(int newDishId)
        {
            dishIdTrackedForDayCount = newDishId;
            currentDishDayCount = GetHighestDishDayFromChecks(newDishId);
            Logger?.LogInfo($"[DishDayCounter] Reset for dish {newDishId} ({GetDishName(newDishId)}), restored count={currentDishDayCount} from AP checks.");
        }

        // Reset the flags when resetting state for a new lobby
        private void ResetStateForLobbyEntry()
        {
            suppressNextDeathLink = false;
            firstCycleCompleted = false;
            franchised = false;
            lost = false;
            stars = 0;
            lastDay = 0;
            dayTransitionProcessed = false;
            itemsSpawnedThisRun = false;
            moneyClampedThisPrep = false;
            dayID = ComputeRunBaseOffset(timesFranchised);
            startingNameApplied = false;

            // Reconstruct dish day count from Archipelago server
            currentDishDayCount = GetHighestDishDayFromChecks(DishId);
            dishIdTrackedForDayCount = DishId;
            Logger?.LogInfo($"[ResetStateForLobbyEntry] Restored dish {DishId} ({GetDishName(DishId)}) day count={currentDishDayCount} from AP checks.");
        }

        // Increments the franchise completion count and sends the franchise location check
        private void IncrementFranchiseAndCheckGoal()
        {
            timesFranchised++;
            Logger.LogWarning("[Debug] Manually incremented franchise counter to " + timesFranchised + ".");

            try
            {
                if (session != null && session.Locations != null)
                {
                    int locId = ComputeFranchiseTimesLocationId(timesFranchised);
                    session.Locations.CompleteLocationChecks(locId);
                    dayID = ComputeRunBaseOffset(timesFranchised);
                    Logger.LogInfo($"[Debug] Sent franchise completion check ID {locId}; next run base offset set to {dayID}.");
                }
                else
                {
                    Logger.LogWarning("[Debug] Session or Locations unavailable; will not send location check.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Debug] Failed to send franchise completion check: " + ex.Message);
            }

            if (goal == 0 && franchiseCount > 0 && timesFranchised >= franchiseCount)
            {
                Logger.LogInfo("[Debug] Franchise goal reached via manual increment. Sending goal complete.");
                SendGoalComplete();
            }
        }

        private void ClampMoneyToCap()
        {
            if (!HasSingleton<SKitchenMarker>())
                return;

            if (Require(out SMoney money))
            {
                int cap = Mathf.Max(0, MoneyCap);
                if (money.Amount > cap)
                {
                    int before = money.Amount;
                    money.Amount = cap;
                    Set(money);
                    Logger?.LogInfo($"[MoneyCap] Clamped money from {before} to {cap} during prep.");
                }
            }
        }

        private int ComputeDeterministicSeed()
        {
            try
            {
                string a = CachedConfig?.address ?? string.Empty;
                string p = (CachedConfig?.port ?? 0).ToString();
                string u = CachedConfig?.playername ?? string.Empty;
                string key = $"{a}|{p}|{u}";
                int h = key.GetHashCode();
                if (h == 0) h = Environment.TickCount;
                return h;
            }
            catch
            {
                return Environment.TickCount;
            }
        }

        private void TryRandomizeUpgradesOnce()
        {
            if (upgradesRandomized)
                return;

            if (!ArchipelagoConnectionManager.ConnectionSuccessful)
            {
                Logger.LogInfo("[Randomizer] Skipping upgrade randomization: not connected to Archipelago.");
                return;
            }

            var data = KitchenData.GameData.Main;
            if (data == null)
            {
                Logger.LogWarning("[Randomizer] GameData.Main not ready; skipping upgrade randomization.");
                return;
            }

            int seed = ComputeDeterministicSeed();
            try
            {
                RandomUpgradeMapper.Apply(data, seed);
                upgradesRandomized = true;
                Logger.LogInfo($"[Randomizer] Applied experimental upgrade randomization with seed={seed}.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[Randomizer] Failed to apply upgrade randomization: " + ex.Message);
            }
        }

        public void TriggerUpgradeRandomizationForDebug()
        {
            var data = KitchenData.GameData.Main;
            if (data == null)
            {
                ChatManager.AddSystemMessage("Upgrade randomization failed: GameData not ready.");
                return;
            }

            int seed = ComputeDeterministicSeed();
            try
            {
                RandomUpgradeMapper.Apply(data, seed);
                upgradesRandomized = true;
                Logger?.LogInfo($"[Randomizer][Debug] Forced upgrade randomization with seed {seed}.");
                ChatManager.AddSystemMessage($"Upgrade pools randomized (seed {seed}).");
            }
            catch (Exception ex)
            {
                Logger?.LogWarning("[Randomizer][Debug] Randomization failed: " + ex.Message);
                ChatManager.AddSystemMessage("Upgrade randomization failed: " + ex.Message);
            }
        }

        private bool TryHandleDishUnlockFromItem(int checkId, string itemName)
        {
            string dishName = null;

            var dishById = ProgressionMapping.dishUnlockIDs.FirstOrDefault(kv => kv.Value == checkId);
            if (!string.IsNullOrEmpty(dishById.Key))
                dishName = dishById.Key;

            if (dishName == null && !string.IsNullOrWhiteSpace(itemName) &&
                itemName.StartsWith("Unlock:", StringComparison.OrdinalIgnoreCase))
            {
                dishName = itemName.Substring("Unlock:".Length).Trim();
            }

            if (string.IsNullOrEmpty(dishName))
                return false;

            int dishGdoId = ProgressionMapping.dishDictionary
                .FirstOrDefault(kv => string.Equals(kv.Value, dishName, StringComparison.OrdinalIgnoreCase))
                .Key;

            if (dishGdoId == 0 || !KitchenData.GameData.Main.TryGet<Dish>(dishGdoId, out _))
            {
                Logger.LogWarning($"[DishUnlock] Could not resolve '{dishName}' to a valid GDO Dish ID.");
                return false;
            }

            PersistUnlockedDish(dishGdoId);
            LockedDishes.AddUnlockedDishes(new[] { dishGdoId });
            LockedDishes.EnableLocking();
            DishId = dishGdoId;

            Logger.LogInfo($"[DishUnlock] Unlocked dish '{dishName}' via item ID {checkId} (GDO ID: {dishGdoId}).");
            return true;
        }

        // Add helper in the partial Mod section (with other helpers)
        private void UpdateRestaurantStartingName()
        {
            if (!ArchipelagoConnectionManager.ConnectionSuccessful)
                return;

            string dishName = GetDishName(DishId);
            bool hasDish = !string.IsNullOrEmpty(dishName) && !string.Equals(dishName, "Unknown", StringComparison.OrdinalIgnoreCase);

            string suffix = hasDish ? dishName : $"Run {timesFranchised + 1}";
            string finalName = $"Archipelago {suffix}";

            if (finalName == lastAppliedStartingName && startingNameApplied)
                return;

            if (finalName.Length > FixedString64.UTF8MaxLengthInBytes)
                finalName = finalName.Substring(0, FixedString64.UTF8MaxLengthInBytes);

            var fs = new FixedString64(finalName);

            if (HasSingleton<SRestaurantStartingName>())
            {
                var name = GetSingleton<SRestaurantStartingName>();
                name.Name = fs;
                Set(name);
            }
            else
            {
                Entity entity = EntityManager.CreateEntity(typeof(SRestaurantStartingName));
                EntityManager.SetComponentData(entity, new SRestaurantStartingName
                {
                    Name = fs
                });
            }

            lastAppliedStartingName = finalName;
            startingNameApplied = true;
            Logger.LogInfo($"[Name] Set starting restaurant name to '{finalName}'.");
        }
    }

    // Properties and methods extracted from Mod class for clarity
    partial class Mod
    {
        // Expose for LeaseRequirementSystem and Chat HUD (eliminates reflection)
        internal static int Goal => goal;
        internal static int OverallDaysCompleted => overallDaysCompleted;
        internal static int DayLeaseInterval => dayLeaseInterval;
        internal int TimesFranchised => timesFranchised;

        internal int ActiveDishId => DishId;

        internal string GetDishName(int dishId) => ProgressionMapping.dishDictionary.TryGetValue(dishId, out var name) ? name : "Unknown";

        private void SetCurrentDish(int newDishId, bool persist = true, bool resetDayCounter = true)
        {
            if (newDishId == 0 || newDishId == DishId)
                return;

            DishId = newDishId;
            lastCardSyncDishId = DishId;

            if (resetDayCounter)
                ResetDishDayCounter(DishId);

            if (persist)
                PersistUnlockedDish(DishId);

            LockedDishes.AddUnlockedDishes(new[] { DishId });
            LockedDishes.EnableLocking();

            Logger.LogInfo($"[Dish] Current dish set to '{GetDishName(DishId)}' (GDO {DishId}).");
        }

        private static void ResetItemsSubscription()
        {
            itemsEventSubscribed = false;
        }
        private int FindHeldDishId()
        {
            if (playersWithItems == null)
                return 0;

            using var players = playersWithItems.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (!EntityManager.Exists(player) || !EntityManager.HasComponent<CItemHolder>(player))
                    continue;

                var holder = EntityManager.GetComponentData<CItemHolder>(player);
                Entity held = holder.HeldItem;
                if (held == Entity.Null || !EntityManager.Exists(held))
                    continue;

                // Dish cards held in hand (progression cards)
                if (EntityManager.HasComponent<CProgressionOption>(held))
                {
                    var option = EntityManager.GetComponentData<CProgressionOption>(held);
                    if (option.ID != 0 && KitchenData.GameData.Main.TryGet<Dish>(option.ID, out _))
                        return option.ID;
                }

                if (EntityManager.HasComponent<CDishUpgrade>(held))
                {
                     var upgrade = EntityManager.GetComponentData<CDishUpgrade>(held);
                     if (upgrade.DishID != 0)
                         return upgrade.DishID;
                }
            }

            return 0;
        }
        private void EnsureDishLockingBaseline()
        {
            // Already have an allowed set
            if (LockedDishes.IsLockingEnabled() && LockedDishes.GetAvailableDishes().Any())
                return;

            // Try persisted last unlocked dish
            int? persisted = LoadPersistedUnlockedDish();
            if (persisted.HasValue && KitchenData.GameData.Main.TryGet<Dish>(persisted.Value, out _))
            {
                LockedDishes.SetUnlockedDishes(new[] { persisted.Value });
                LockedDishes.EnableLocking();
                SetCurrentDish(persisted.Value, persist: false, resetDayCounter: false);
                Logger.LogWarning($"[LockedDishes] Fallback baseline applied from persisted dish {persisted.Value}.");
                return;
            }

            // No baseline available; leave locking disabled to avoid nuking HQ content
            LockedDishes.DisableLocking();
            Logger.LogWarning("[LockedDishes] No baseline dish found; locking remains disabled.");
        }

        private void ReapplyMoneyCapFromHistory()
        {
            if (session?.Items?.AllItemsReceived == null)
                return;

            int upgradeCount = session.Items.AllItemsReceived.Count(item => (int)item.ItemId == 16);
            MoneyCap = Mathf.Clamp(baseMoneyCap + upgradeCount * MoneyCapIncrementStep, 0, 999);
            moneyClampPending = true;
            Logger.LogInfo($"[MoneyCap] Re-applied cap (base={baseMoneyCap}, upgrades={upgradeCount}) => {MoneyCap}");
        }
        private void SyncDishFromActiveCards()
        {
            if (progressionUnlockQuery == null || !HasSingleton<SKitchenMarker>())
                return;

            using var unlocks = progressionUnlockQuery.ToComponentDataArray<CProgressionUnlock>(Allocator.Temp);
            if (unlocks.Length == 0)
                return;

            int foundDish = 0;
            for (int i = 0; i < unlocks.Length; i++)
            {
                int id = unlocks[i].ID;
                if (id == 0)
                    continue;

                if (ProgressionMapping.dishDictionary.ContainsKey(id))
                {
                    foundDish = id;
                    break;
                }
            }

            if (foundDish == 0)
                return;

            // Only log/adopt when the active card dish differs from current and from the last synced card dish.
            if (foundDish != DishId && foundDish != lastCardSyncDishId)
            {
                Logger.LogInfo($"[Dish Sync] Adopting active card dish {foundDish} ({GetDishName(foundDish)}); previous local dish={DishId} ({GetDishName(DishId)}).");
                SetCurrentDish(foundDish, persist: false, resetDayCounter: false);
            }
            else
            {
                // Keep tracking to avoid repeated logs when the dish is unchanged.
                lastCardSyncDishId = foundDish;
            }
        }

        private void LogPrepDishSnapshot()
        {
            int kitchenDish = 0;
            if (HasSingleton<RebuildKitchen.SCurrentKitchen>())
            {
                kitchenDish = GetSingleton<RebuildKitchen.SCurrentKitchen>().Dish;
            }

            string kitchenDishName = GetDishName(kitchenDish);
            string localDishName = GetDishName(DishId);

            Logger.LogInfo($"[Prep Dish] Local DishId={DishId} ({localDishName}), Kitchen Dish={kitchenDish} ({kitchenDishName}), DishDayCount={currentDishDayCount}");

            if (kitchenDish != 0 && kitchenDish != DishId)
            {
                Logger.LogWarning($"[Prep Dish] Mismatch detected. Updating current dish to kitchen singleton: {kitchenDish} ({kitchenDishName}).");
                SetCurrentDish(kitchenDish);
            }
        }
        private void SpawnExtraBlueprints()
        {
            var pool = new List<int>();
            foreach (var appliance in KitchenData.GameData.Main.Get<Appliance>())
            {
                if (!appliance.IsPurchasable)
                    continue;
                if (ApplianceUnlocksEnabled && !IsApplianceUnlocked(appliance.ID))
                    continue;
                pool.Add(appliance.ID);
            }

            if (pool.Count == 0)
            {
                Logger.LogWarning("[ShopExpansion] No valid appliances in pool.");
                return;
            }

            int toSpawn = Mathf.Min(ExtraBlueprintCount, pool.Count);

            // Shuffle
            var rng = new System.Random();
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            Vector3 basePos = SpawnHelpers.ResolveSpawnPosition(EntityManager, Spawning.SpawnPositionType.Door, InputSourceIdentifier.Identifier);

            for (int i = 0; i < toSpawn; i++)
            {
                int gdoId = pool[i];
                Vector3 offset = new Vector3(i * 0.5f, 0f, 0f);
                SpawnHelpers.TrySpawnApplianceBlueprint(EntityManager, gdoId, basePos + offset, costMode: 1f);
                Logger.LogInfo($"[ShopExpansion] Spawned extra blueprint GDO={gdoId} ({i + 1}/{toSpawn})");
            }

            itemsSpawnedThisRun = true;
            Logger.LogInfo($"[ShopExpansion] Spawned {toSpawn} extra blueprint(s) this prep.");
        }

        /// <summary>
        /// Goal 2: Counts active dishes as 1 (starting dish) + received dish unlock items.
        /// </summary>
        private int CountActiveDishes()
        {
            if (session?.Items?.AllItemsReceived == null)
                return 1;

            var dishUnlockIds = new HashSet<int>(ProgressionMapping.dishUnlockIDs.Values);
            int unlockCount = session.Items.AllItemsReceived.Count(item => dishUnlockIds.Contains((int)item.ItemId));
            return 1 + unlockCount; // 1 for starting dish
        }
    }
}