using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Kitchen;
using KitchenLib;
using KitchenLib.Logging;
using KitchenLib.Utils;
using KitchenLib.References;
using KitchenMods;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using KitchenData;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using KitchenDecorOnDemand;
using PreferenceSystem;
using PreferenceSystem.Event;
using PreferenceSystem.Menus;

namespace PlateupAP
{
    // Custom JSON converter.
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

    // Simple wrapper to set an object's position.
    public class PositionWrapper : MonoBehaviour
    {
        public void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }
    }

    public class ArchipelagoConfig
    {
        public string address { get; set; }
        public string port { get; set; }
        public string playername { get; set; }
        public string password { get; set; }
    }

    public class Mod : BaseMod, IModSystem
    {
        public const string MOD_GUID = "com.caz.plateupap";
        public const string MOD_NAME = "PlateupAP";
        public const string MOD_VERSION = "0.1.4";
        public const string MOD_AUTHOR = "Caz";
        public const string MOD_GAMEVERSION = ">=1.1.9";

        internal static AssetBundle Bundle = null;
        internal static KitchenLib.Logging.KitchenLogger Logger;

        // Archipelago connection fields.
        private ArchipelagoSession session;
        private string ip = "archipelago.gg";
        private int port;
        private string playerName;
        private string password;
        private string configFilePath;

        // Reconnection control.
        private bool connectionSuccessful = false;
        private float reconnectDelay = 10f;
        private float lastAttemptTime = -100f;

        // Day cycle fields.
        private bool dayPhase;
        private bool prepPhase;
        private int lastDay = 0;
        private int dayID = 100000;
        private int timesFranchised = 1;
        private bool loggedDayTransition = false;
        private bool loggedPrepTransition = false;
        private bool itemsEventSubscribed = false;

        public static class InputSourceIdentifier
        {
            public static int Identifier = 0;
        }

        // Mapping from progression check ids to in-game GDO ids.
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

        // Queue to store received items until prep phase.
        private Queue<ItemInfo> spawnQueue = new Queue<ItemInfo>();

        public Mod() : base(MOD_GUID, MOD_NAME, MOD_AUTHOR, MOD_VERSION, MOD_GAMEVERSION, Assembly.GetExecutingAssembly())
        {
        }

        static PreferenceSystemManager PrefManager;

        protected override void OnPostActivate(KitchenMods.Mod mod)
        {
            // Initialize the PreferenceSystemManager.
            PrefManager = new PreferenceSystemManager(MOD_GUID, MOD_NAME);

            // Set up the config file path in a user-writable folder (persistentDataPath).
            SetupConfigFilePath();

            // Build the in-game menu.
            PrefManager
                .AddLabel("Archipelago Connection")
                .AddButton("Create Config File", _ => CreateConfigFileButton(), 0, 1f, 0.2f)
                .AddButton("Connect", _ => OnConnectPressed(), 0, 1f, 0.2f);

            // Register the menu to appear in the main menu.
            PrefManager.RegisterMenu(PreferenceSystemManager.MenuType.MainMenu);
        }

        private void SetupConfigFilePath()
        {
            // Ensure Logger is initialized.
            if (Logger == null)
            {
                Logger = InitLogger();
            }

            // Use Unity's persistentDataPath which is writable and user-specific.
            string configFolder = Application.persistentDataPath;
            // Create a subfolder "PlateUpAPConfig" within the persistent data folder.
            configFolder = Path.Combine(configFolder, "PlateUpAPConfig");
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
                Logger.LogInfo("Created config folder: " + configFolder);
            }
            else
            {
                Logger.LogInfo("Config folder already exists: " + configFolder);
            }
            // Set the config file path.
            configFilePath = Path.Combine(configFolder, "archipelago_config.json");
            Logger.LogInfo("Config file path set to: " + configFilePath);
        }


        private void EnsureConfigFileExists()
        {
            if (!File.Exists(configFilePath))
            {
                ArchipelagoConfig defaultConfig = new ArchipelagoConfig
                {
                    address = "archipelago.gg", // default address now set
                    port = "",
                    playername = "",
                    password = ""
                };

                JsonSerializerSettings localSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Converters = new List<JsonConverter>() // no converters
                };

                string json;
                using (StringWriter sw = new StringWriter())
                {
                    JsonSerializer serializer = JsonSerializer.Create(localSettings);
                    serializer.Serialize(sw, defaultConfig);
                    json = sw.ToString();
                }
                File.WriteAllText(configFilePath, json);
                Logger.LogInfo("Created configuration file at: " + configFilePath);
            }
            else
            {
                Logger.LogInfo("Configuration file already exists at: " + configFilePath);
            }
        }



        private void CreateConfigFileButton()
        {
            try
            {
                EnsureConfigFileExists();
                Logger.LogInfo("Create Config File button pressed. Config file is located at: " + configFilePath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to create config file: " + ex);
            }
        }

        private ArchipelagoConfig LoadConfig()
        {
            EnsureConfigFileExists();
            string json = File.ReadAllText(configFilePath);
            Logger.LogInfo("Raw config JSON: " + json);

            ArchipelagoConfig config;
            using (StringReader sr = new StringReader(json))
            {
                JsonSerializer serializer = new JsonSerializer();
                // Ensure no converters from global defaults interfere.
                serializer.Converters.Clear();
                config = (ArchipelagoConfig)serializer.Deserialize(sr, typeof(ArchipelagoConfig));
            }

            Logger.LogInfo("Successfully loaded configuration.");
            return config;
        }



        private void OnConnectPressed()
        {
            Logger.LogInfo("Connect button pressed.");
            try
            {
                ArchipelagoConfig config = LoadConfig();
                Logger.LogInfo("Config loaded. Fields:");
                Logger.LogInfo("  address: " + config.address);
                Logger.LogInfo("  port: " + config.port);
                Logger.LogInfo("  playername: " + config.playername);
                Logger.LogInfo("  password: " + config.password);

                string ipAddress = string.IsNullOrWhiteSpace(config.address) ? "archipelago.gg" : config.address;
                string portStr = config.port;
                string player = config.playername;
                string pwd = config.password;

                if (string.IsNullOrWhiteSpace(portStr) || !int.TryParse(portStr, out int portNumber))
                {
                    Logger.LogError("Port is not set or invalid in the configuration file. Please update the file at: " + configFilePath);
                    return;
                }

                ip = ipAddress;
                port = portNumber;
                playerName = player;
                password = pwd;

                Logger.LogInfo("Attempting connection with settings:");
                Logger.LogInfo("  Address: " + ip);
                Logger.LogInfo("  Port: " + port);
                Logger.LogInfo("  Player Name: " + playerName);

                TryConnectToArchipelago();
            }
            catch (Exception ex)
            {
                Logger.LogError("Exception in OnConnectPressed: " + ex);
            }
        }

        protected override void OnInitialise()
        {
            Logger = InitLogger();
            Logger.LogWarning($"{MOD_GUID} v{MOD_VERSION} in use!");

            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new SafeStringListConverter() }
            };
        }

        protected override void OnUpdate()
        {
            if (connectionSuccessful && HasSingleton<SKitchenMarker>())
            {
                UpdateDayCycle();
                CheckReceivedItems();
            }
        }

        private void CheckReceivedItems()
        {
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

            if (!prepPhase)
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

        private void UpdateDayCycle()
        {
            if (connectionSuccessful && session != null)
            {
                bool isDayStart = HasSingleton<SIsDayFirstUpdate>();
                bool isPrepPhase = HasSingleton<SIsNightTime>();
                bool franchiseScreen = HasSingleton<SFranchiseBuilderMarker>();
                bool loseScreen = HasSingleton<SPostgameFirstFrameMarker>();

                if (isPrepPhase)
                {
                    prepPhase = true;
                    dayPhase = false;
                    Logger.LogInfo("It's time to prep for the next day!");
                    while (spawnQueue.Count > 0)
                    {
                        ItemInfo queuedInfo = spawnQueue.Dequeue();
                        ProcessSpawn(queuedInfo);
                    }
                }
                if (isDayStart)
                {
                    lastDay++;
                    prepPhase = false;
                    dayPhase = true;
                    Logger.LogInfo("Transitioning from night to day, advancing day count...");
                    session.Locations.CompleteLocationChecks(dayID + lastDay);
                    int presentdayID = dayID + lastDay;
                    Logger.LogInfo("Day Logged " + lastDay + " with ID " + presentdayID);
                }
                if (franchiseScreen)
                {
                    Logger.LogInfo("You franchised!");
                    timesFranchised++;
                    dayID = 100000 * timesFranchised;

                    session.Locations.CompleteLocationChecks(dayID);

                    lastDay = 0;
                    franchiseScreen = false;
                }
                
            
                else if (loseScreen)
                {
                    Logger.LogInfo("You Lost the Run!");
                    lastDay = 0;
                    session.Locations.CompleteLocationChecks(dayID);
                }
            }
        }


        private void TryConnectToArchipelago()
        {
            Logger.LogInfo("Attempting connection to Archipelago...");
            string connectionUrl = $"wss://{ip}:{port}/";
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

        private void Socket_ErrorReceived(Exception e, string message)
        {
            Logger.LogInfo("Socket Error: " + message);
            Logger.LogInfo("Socket Exception: " + e.Message);
            if (e.StackTrace != null)
            {
                foreach (var line in e.StackTrace.Split('\n'))
                    Logger.LogInfo("    " + line);
            }
            else
            {
                Logger.LogInfo("    No stacktrace provided");
            }
        }
        private void Socket_SocketOpened()
        {
            Logger.LogInfo("Socket opened to: " + session.Socket.Uri);
        }
        private void Socket_SocketClosed(string reason)
        {
            Logger.LogInfo("Socket closed: " + reason);
        }
    }
}
