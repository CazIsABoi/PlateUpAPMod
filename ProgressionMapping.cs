using KitchenData;
using KitchenLib.References;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace KitchenPlateupAP
{
    public static class ProgressionMapping
    {
        public static readonly Dictionary<int, int> progressionToGDO = new Dictionary<int, int>()
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

        public static readonly Dictionary<int, int> applianceUnlockToGDO = new Dictionary<int, int>
        {
            { 2001, ApplianceReferences.Hob },
            { 2002, ApplianceReferences.HobSafe },
            { 2003, ApplianceReferences.HobDanger },
            { 2004, ApplianceReferences.Oven },
            { 2005, ApplianceReferences.Microwave },
            { 2006, ApplianceReferences.SinkNormal },
            { 2007, ApplianceReferences.SinkPower },
            { 2008, ApplianceReferences.SinkSoak },
            { 2009, ApplianceReferences.DishWasher },
            { 2010, ApplianceReferences.SinkLarge },
            { 2011, ApplianceReferences.Countertop },
            { 2012, ApplianceReferences.Workstation },
            { 2013, ApplianceReferences.Freezer },
            { 2014, ApplianceReferences.PrepStation },
            { 2015, ApplianceReferences.FrozenPrepStation },
            { 2016, ApplianceReferences.BlueprintUpgradeDesk },
            { 2017, ApplianceReferences.BlueprintCopyDesk },
            { 2018, ApplianceReferences.BlueprintDiscountDesk },
            { 2019, ApplianceReferences.BlueprintOrderingDesk },
            { 2020, ApplianceReferences.Grabber },
            { 2021, ApplianceReferences.GrabberSmart },
            { 2022, ApplianceReferences.GrabberRotatable },
            { 2023, ApplianceReferences.Combiner },
            { 2024, ApplianceReferences.Portioner },
            { 2025, ApplianceReferences.Mixer },
            { 2026, ApplianceReferences.MixerPusher },
            { 2027, ApplianceReferences.MixerHeated },
            { 2028, ApplianceReferences.MixerRapid },
            { 2029, ApplianceReferences.AutoPlater },
            { 2030, ApplianceReferences.PotStack },
            { 2031, ApplianceReferences.ServingBoardStack },
            { 2032, ApplianceReferences.IceDispenser },
            { 2033, ApplianceReferences.MilkDispenser },
            { 2034, ApplianceReferences.MopBucket },
            { 2035, ApplianceReferences.MopBucketLasting },
            { 2036, ApplianceReferences.MopBucketFast },
            { 2037, ApplianceReferences.RobotMop },
            { 2038, ApplianceReferences.FloorBufferStation },
            { 2039, ApplianceReferences.RobotBuffer },
            { 2040, ApplianceReferences.BreadstickBox },
            { 2041, ApplianceReferences.CandleBox },
            { 2042, ApplianceReferences.NapkinBox },
            { 2043, ApplianceReferences.SharpCutlery },
            { 2044, ApplianceReferences.SpecialsMenuBox },
            { 2045, ApplianceReferences.LeftoversBagStation },
            { 2046, ApplianceReferences.SupplyCabinet },
            { 2047, ApplianceReferences.HostStand },
            { 2048, ApplianceReferences.FlowerPot },
            { 2049, ApplianceReferences.CoffeeTable },
            { 2050, ApplianceReferences.FoodDisplayStand },
            { 2051, ApplianceReferences.FireExtinguisherHolder },
            { 2052, ApplianceReferences.PlateStack },
            { 2053, ApplianceReferences.PlateStackStarting },
            { 2054, ApplianceReferences.WokStack },
            { 2055, ApplianceReferences.SourceLasagneTray },
            { 2056, ApplianceReferences.ProviderTacoTray },
            { 2057, ApplianceReferences.ProviderMixingBowls },
            { 2058, ApplianceReferences.SourceBigCakeTin },
            { 2059, ApplianceReferences.SourceBrownieTray },
            { 2060, ApplianceReferences.SourceCookieTray },
            { 2061, ApplianceReferences.SourceCupcakeTray },
            { 2062, ApplianceReferences.SourceDoughnutTray },
        };

        public static readonly List<int> usefulApplianceGDOs = new List<int>
        {
            ApplianceReferences.Hob,
            ApplianceReferences.HobSafe,
            ApplianceReferences.HobDanger,
            ApplianceReferences.HobStarting,
            ApplianceReferences.Oven,
            ApplianceReferences.Microwave,
            ApplianceReferences.SinkNormal,
            ApplianceReferences.SinkPower,
            ApplianceReferences.SinkSoak,
            ApplianceReferences.SinkStarting,
            ApplianceReferences.DishWasher,
            ApplianceReferences.SinkLarge,
            ApplianceReferences.Countertop,
            ApplianceReferences.Workstation,
            ApplianceReferences.Freezer,
            ApplianceReferences.PrepStation,
            ApplianceReferences.FrozenPrepStation,
            ApplianceReferences.BlueprintUpgradeDesk,
            ApplianceReferences.BlueprintCopyDesk,
            ApplianceReferences.BlueprintDiscountDesk,
            ApplianceReferences.BlueprintOrderingDesk,
            ApplianceReferences.Grabber,
            ApplianceReferences.GrabberSmart,
            ApplianceReferences.GrabberRotatable,
            ApplianceReferences.Combiner,
            ApplianceReferences.Portioner,
            ApplianceReferences.Mixer,
            ApplianceReferences.MixerPusher,
            ApplianceReferences.MixerHeated,
            ApplianceReferences.MixerRapid,
            ApplianceReferences.AutoPlater,
            ApplianceReferences.PotStack,
            ApplianceReferences.ServingBoardStack
        };

        public static readonly List<int> fillerApplianceGDOs = new List<int>
        {
            ApplianceReferences.SinkStarting,
            ApplianceReferences.IceDispenser,
            ApplianceReferences.MilkDispenser,
            ApplianceReferences.MopBucket,
            ApplianceReferences.MopBucketLasting,
            ApplianceReferences.MopBucketFast,
            ApplianceReferences.RobotMop,
            ApplianceReferences.FloorBufferStation,
            ApplianceReferences.RobotBuffer,
            ApplianceReferences.BreadstickBox,
            ApplianceReferences.CandleBox,
            ApplianceReferences.NapkinBox,
            ApplianceReferences.SharpCutlery,
            ApplianceReferences.SpecialsMenuBox,
            ApplianceReferences.LeftoversBagStation,
            ApplianceReferences.SupplyCabinet,
            ApplianceReferences.HostStand,
            ApplianceReferences.FlowerPot,
            ApplianceReferences.CoffeeTable,
            ApplianceReferences.FoodDisplayStand,
            ApplianceReferences.FireExtinguisherHolder,
            ApplianceReferences.PlateStack,
            ApplianceReferences.PlateStackStarting
        };

        public static readonly Dictionary<string, int> usefulApplianceDictionary = new Dictionary<string, int>
        {
            { "Hob", ApplianceReferences.Hob },
            { "Hob (Safe)", ApplianceReferences.HobSafe },
            { "Hob (Danger)", ApplianceReferences.HobDanger },
            { "Oven", ApplianceReferences.Oven },
            { "Microwave", ApplianceReferences.Microwave },
            { "Sink", ApplianceReferences.SinkNormal },
            { "Power Sink", ApplianceReferences.SinkPower },
            { "Soaking Sink", ApplianceReferences.SinkSoak },
            { "Dishwasher", ApplianceReferences.DishWasher },
            { "Large Sink", ApplianceReferences.SinkLarge },
            { "Counter", ApplianceReferences.Countertop },
            { "Workstation", ApplianceReferences.Workstation },
            { "Freezer", ApplianceReferences.Freezer },
            { "Prep Station", ApplianceReferences.PrepStation },
            { "Frozen Prep Station", ApplianceReferences.FrozenPrepStation },
            { "Research Desk", ApplianceReferences.BlueprintUpgradeDesk },
            { "Copy Desk", ApplianceReferences.BlueprintCopyDesk },
            { "Discount Desk", ApplianceReferences.BlueprintDiscountDesk },
            { "Ordering Desk", ApplianceReferences.BlueprintOrderingDesk },
            { "Grabber", ApplianceReferences.Grabber },
            { "Smart Grabber", ApplianceReferences.GrabberSmart },
            { "Rotatable Grabber", ApplianceReferences.GrabberRotatable },
            { "Combiner", ApplianceReferences.Combiner },
            { "Portioner", ApplianceReferences.Portioner },
            { "Mixer", ApplianceReferences.Mixer },
            { "Mixer (Pusher)", ApplianceReferences.MixerPusher },
            { "Heated Mixer", ApplianceReferences.MixerHeated },
            { "Rapid Mixer", ApplianceReferences.MixerRapid },
            { "Auto Plater", ApplianceReferences.AutoPlater },
        };

        public static readonly Dictionary<string, int> fillerApplianceDictionary = new Dictionary<string, int>
        {
            { "Ice Dispenser", ApplianceReferences.IceDispenser },
            { "Milk Dispenser", ApplianceReferences.MilkDispenser },
            { "Mop Bucket", ApplianceReferences.MopBucket },
            { "Lasting Mop", ApplianceReferences.MopBucketLasting },
            { "Fast Mop", ApplianceReferences.MopBucketFast },
            { "Robot Mop", ApplianceReferences.RobotMop },
            { "Floor Buffer", ApplianceReferences.FloorBufferStation },
            { "Robot Buffer", ApplianceReferences.RobotBuffer },
            { "Breadsticks", ApplianceReferences.BreadstickBox },
            { "Candles", ApplianceReferences.CandleBox },
            { "Napkins", ApplianceReferences.NapkinBox },
            { "Sharp Cutlery", ApplianceReferences.SharpCutlery },
            { "Specials Menu", ApplianceReferences.SpecialsMenuBox },
            { "Leftovers Bag", ApplianceReferences.LeftoversBagStation },
            { "Supply Cabinet", ApplianceReferences.SupplyCabinet },
            { "Host Stand", ApplianceReferences.HostStand },
            { "Flower Pot", ApplianceReferences.FlowerPot },
            { "Coffee Table", ApplianceReferences.CoffeeTable },
            { "Food Display", ApplianceReferences.FoodDisplayStand },
            { "Fire Extinguisher Holder", ApplianceReferences.FireExtinguisherHolder },
            { "Plate Stack", ApplianceReferences.PlateStack },
            { "Starting Plate Stack", ApplianceReferences.PlateStackStarting },
            { "Wok Stack", ApplianceReferences.WokStack },
            { "Lasagne Tray", ApplianceReferences.SourceLasagneTray },
            { "Taco Tray", ApplianceReferences.ProviderTacoTray },
            { "Mixing Bowls", ApplianceReferences.ProviderMixingBowls },
            { "Big Cake Tin", ApplianceReferences.SourceBigCakeTin },
            { "Brownie Tray", ApplianceReferences.SourceBrownieTray },
            { "Cookie Tray", ApplianceReferences.SourceCookieTray },
            { "Cupcake Tray", ApplianceReferences.SourceCupcakeTray },
            { "Doughnut Tray", ApplianceReferences.SourceDoughnutTray },
            { "Pot Stack", ApplianceReferences.PotStack },
            { "Serving Board Stack", ApplianceReferences.ServingBoardStack },
        };

        public static readonly Dictionary<string, int> decorDictionary = new Dictionary<string, int>
        {
            { "Affordable Affordable Bin", ApplianceReferences.AffordableBin },
            { "Affordable Dirty Floor Sign", ApplianceReferences.AffordableWetFloorSign },
            { "Affordable Gumball Machine", ApplianceReferences.AffordableGumballMachine },
            { "Affordable Stock Picture", ApplianceReferences.AffordableStockArt },
            { "Affordable Ceiling Light", ApplianceReferences.AffordableRoofLight },
            { "Affordable Neon Sign Eat", ApplianceReferences.AffordableNeonSign1 },
            { "Affordable Neon Sign Enjoy", ApplianceReferences.AffordableNeonSign2 },
            { "Charming Dartboard", ApplianceReferences.CosyDartboard },
            { "Charming Wall Light", ApplianceReferences.CosyWallLight },
            { "Charming Barrel", ApplianceReferences.CosyBarrel},
            { "Charming Bookcase", ApplianceReferences.CosyBookcase },
            { "Charming Rug", ApplianceReferences.CosyRug },
            { "Charming Fireplace", ApplianceReferences.CosyFireplace },
            { "Exclusive Candelabra", ApplianceReferences.FancyCandelabra },
            { "Exclusive Chandelier", ApplianceReferences.FancyChandelier },
            { "Exclusive Painting", ApplianceReferences.FancyPainting },
            { "Exclusive Rug", ApplianceReferences.FancyRug },
            { "Exclusive Classical Globe", ApplianceReferences.FancyGlobe },
            { "Exclusive Precious Flower", ApplianceReferences.FancyFlowers },
            { "Exclusive Statue", ApplianceReferences.FancyStatue },
            { "Formal Abstract Lamp", ApplianceReferences.FormalStandingLamp },
            { "Formal Tidy Plant", ApplianceReferences.FormalPlant },
            { "Formal Vase", ApplianceReferences.FormalVase },
            { "Formal Brand Mascot", ApplianceReferences.FormalDogStatue },
            { "Formal Indoor Fountain", ApplianceReferences.Fountain },
            { "Miscellaneous Calm Painting", ApplianceReferences.Painting },
            { "Miscellaneous Plant", ApplianceReferences.Plant },
            { "Miscellaneous Rug", ApplianceReferences.Rug },
        };

        public static readonly Dictionary<int, int> customerCardDictionary = new Dictionary<int, int>()
        {
            { 1, UnlockCardReferences.Affordable },
            { 2, UnlockCardReferences.AllYouCanEat },
            { 3, UnlockCardReferences.AllYouCanEatIncrease },
            { 4, UnlockCardReferences.ChangeOrdersAfterOrdering },
            { 5, UnlockCardReferences.Couples },
            { 6, UnlockCardReferences.ClosingTime },
            { 7, UnlockCardReferences.CustomerBursts },
            { 8, UnlockCardReferences.CustomersEatSlowly },
            { 9, UnlockCardReferences.CustomersRequireWalking },
            { 10, UnlockCardReferences.DinnerRush },
            { 11, UnlockCardReferences.DoubleDates },
            { 12, UnlockCardReferences.FirstDates },
            { 13, UnlockCardReferences.FlexibleDining },
            { 14, UnlockCardReferences.HiddenOrders },
            { 15, UnlockCardReferences.HiddenPatience },
            { 16, UnlockCardReferences.HiddenProcesses },
            { 17, UnlockCardReferences.IndividualDining },
            { 18, UnlockCardReferences.InstantOrders },
            { 19, UnlockCardReferences.LargeGroups },
            { 20, UnlockCardReferences.LessMoney },
            { 21, UnlockCardReferences.LosePatienceInView },
            { 22, UnlockCardReferences.LunchRush },
            { 23, UnlockCardReferences.MediumGroups },
            { 24, UnlockCardReferences.MessesSlowCustomers },
            { 25, UnlockCardReferences.MessRangeIncrease },
            { 26, UnlockCardReferences.MessyCustomers },
            { 27, UnlockCardReferences.MoreCustomers },
            { 28, UnlockCardReferences.MoreCustomers2 },
            { 29, UnlockCardReferences.MorningRush },
            { 30, UnlockCardReferences.PatienceDecrease },
            { 31, UnlockCardReferences.PickyEaters },
            { 32, UnlockCardReferences.QuickerBurning },
            { 33, UnlockCardReferences.SlowProcesses },
            { 34, UnlockCardReferences.TippingCulture },
        };

        public static readonly Dictionary<int, int> easydifficultCardDictionary = new Dictionary<int, int>()
        {
            { 1, UnlockCardReferences.MoreCustomers },
            { 2, UnlockCardReferences.PatienceDecrease },
            { 3, UnlockCardReferences.ClosingTime },
            { 4, UnlockCardReferences.MessyCustomers },
        };

        public static readonly Dictionary<int, int> difficultCardDictionary = new Dictionary<int, int>()
        {
            { 5, UnlockCardReferences.PickyEaters },
            { 6, UnlockCardReferences.AllYouCanEat },
            { 7, UnlockCardReferences.AllYouCanEatIncrease },
            { 8, UnlockCardReferences.HiddenPatience },
        };

        public static readonly Dictionary<int, string> trapDictionary = new Dictionary<int, string>()
        {
            { 20000, "EVERYTHING IS ON FIRE" },
            { 20001, "Super Slow" },
            { 20002, "Random Customer Card" }
        };

        public static readonly Dictionary<int, string> dishDictionary = new Dictionary<int, string>()
        {
            { DishReferences.SaladBase, "Salad" },
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
            { -1272159363, "Sandwiches" },
            { 934171642, "Sundaes" },
        };

        public static readonly Dictionary<string, int> dish_id_lookup = new Dictionary<string, int>
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
            { "Stir Fry", 115 },
            { "Sandwiches", 116 },
            { "Sundaes", 117 }
        };

        // Display-name -> block index (for location IDs)
        public static readonly Dictionary<string, int> settingBlockIndex = new Dictionary<string, int>
        {
            { "Base Setting", 0 },
            { "Autumn", 1 },
            { "Banquet", 2 },
            { "Turbo", 3 },
            { "Witch Hut", 4 }
        };

        public static readonly Dictionary<int, string> settingIdToDisplay = new Dictionary<int, string>
        {
            { RestaurantSettingReferences.Country, "Base Setting" },
            { RestaurantSettingReferences.Alpine, "Base Setting" },
            { RestaurantSettingReferences.City, "Base Setting" },
            { RestaurantSettingReferences.Autumn, "Autumn" },
            { 206822591, "Banquet" },
            { RestaurantSettingReferences.MarchSettingTurbo, "Turbo" },
            { RestaurantSettingReferences.WitchHut2310, "Witch Hut" }
        };

        public static bool TryResolveSettingDisplay(int settingId, out string displayName)
        {
            return settingIdToDisplay.TryGetValue(settingId, out displayName);
        }

        public static bool TryComputeSettingLocationId(int settingId, int day, out int locId)
        {
            locId = 0;
            if (day < 1 || day > 15)
                return false;

            if (!settingIdToDisplay.TryGetValue(settingId, out string display))
                return false;

            if (!settingBlockIndex.TryGetValue(display, out int block))
                return false;

            locId = 130000 + (block * 100) + day;
            return true;
        }

        public static readonly Dictionary<int, string> speedUpgradeMapping = new Dictionary<int, string>()
        {
            { 10, "Speed Upgrade Player" },
            { 11, "Speed Upgrade Appliance" },
            { 12, "Speed Upgrade Cook" },
            { 13, "Speed Upgrade Chop" },
            { 14, "Speed Upgrade Clean" }
        };

        public static readonly Dictionary<string, int> dishUnlockIDs = new Dictionary<string, int>
        {
            { "Salad", 30101 },
            { "Steak", 30102 },
            { "Burger", 30103 },
            { "Coffee", 30104 },
            { "Pizza", 30105 },
            { "Dumplings", 30106 },
            { "Turkey", 30107 },
            { "Pie", 30108 },
            { "Cakes", 30109 },
            { "Spaghetti", 30110 },
            { "Fish", 30111 },
            { "Tacos", 30112 },
            { "Hot Dogs", 30113 },
            { "Breakfast", 30114 },
            { "Stir Fry", 30115 },
            { "Sandwiches", 30116 },
            { "Sundaes", 30117 }
        };

        static ProgressionMapping()
        {
            TryLoadCustomUsefulAppliances();
        }

        private static void TryLoadCustomUsefulAppliances()
        {
            try
            {
                string folder = Path.Combine(UnityEngine.Application.persistentDataPath, "PlateUpAPConfig");
                string path = Path.Combine(folder, "custom_appliances.json");

                if (!File.Exists(path))
                    return;

                string json = File.ReadAllText(path);
                var ids = JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>();
                if (ids.Count == 0)
                    return;

                foreach (int gdoId in ids)
                {
                    if (gdoId == 0)
                        continue;

                    bool exists =
                        KitchenData.GameData.Main.TryGet<KitchenData.Appliance>(gdoId, out _) ||
                        KitchenData.GameData.Main.TryGet<KitchenData.Decor>(gdoId, out _);

                    if (!exists)
                        continue;

                    string key = $"Custom_{gdoId}";

                    if (!usefulApplianceDictionary.ContainsKey(key))
                    {
                        usefulApplianceDictionary[key] = gdoId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[ProgressionMapping] Failed to load custom appliances: {ex.Message}");
            }
        }
    }
}