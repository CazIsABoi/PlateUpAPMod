using KitchenData;
using KitchenData.Workshop;
using KitchenLib.References;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
            { 10164, ApplianceReferences.ExtraLife },
        };

        public static readonly Dictionary<int, int> applianceUnlockToGDO = new Dictionary<int, int>
        {
            // ── Priority ──────────────────────────────────────────────────────────
            { 60001, ApplianceReferences.Hob },
            { 60002, ApplianceReferences.Countertop },
            { 60003, ApplianceReferences.SinkNormal },
            { 60004, ApplianceReferences.PlateStackStarting },
            { 60005, ApplianceReferences.BlueprintUpgradeDesk },   // Research Desk
            { 60006, ApplianceReferences.Belt },
            { 60007, ApplianceReferences.Grabber },
            // ── Useful appliances ─────────────────────────────────────────────────
            { 60008, ApplianceReferences.HobSafe },
            { 60009, ApplianceReferences.HobDanger },
            { 60010, ApplianceReferences.Oven },
            { 60011, ApplianceReferences.Microwave },
            { 60012, ApplianceReferences.GasLimiter },
            { 60013, ApplianceReferences.GasSafetyOverride },
            { 60014, ApplianceReferences.SinkPower },
            { 60015, ApplianceReferences.SinkSoak },
            { 60016, ApplianceReferences.DishWasher },
            { 60017, ApplianceReferences.SinkLarge },
            { 60018, ApplianceReferences.Workstation },
            { 60019, ApplianceReferences.Freezer },
            { 60020, ApplianceReferences.PrepStation },
            { 60021, ApplianceReferences.FrozenPrepStation },
            { 60022, ApplianceReferences.TableLarge },
            { 60023, ApplianceReferences.TableBar },
            { 60024, ApplianceReferences.TableBasicCloth },
            { 60025, ApplianceReferences.TableCheapMetal },
            { 60026, ApplianceReferences.TableFancyCloth },
            { 60027, ApplianceReferences.RollingPinProvider },
            { 60028, ApplianceReferences.SharpKnifeProvider },
            { 60029, ApplianceReferences.ScrubbingBrushProvider },
            { 60030, ApplianceReferences.ShoeRackTrainers },
            { 60031, ApplianceReferences.BlueprintCopyDesk },
            { 60032, ApplianceReferences.BlueprintDiscountDesk },
            { 60033, ApplianceReferences.BlueprintOrderingDesk },
            { 60034, ApplianceReferences.BookingDesk },
            { 60035, ApplianceReferences.Dumbwaiter },
            { 60036, ApplianceReferences.Teleporter },
            { 60037, ApplianceReferences.OrderingTerminal },
            { 60038, ApplianceReferences.OrderingTerminalSpecialOffers },
            { 60039, ApplianceReferences.GrabberSmart },
            { 60040, ApplianceReferences.GrabberRotatable },
            { 60041, ApplianceReferences.Combiner },
            { 60042, ApplianceReferences.Portioner },
            { 60043, ApplianceReferences.Mixer },
            { 60044, ApplianceReferences.MixerPusher },
            { 60045, ApplianceReferences.MixerHeated },
            { 60046, ApplianceReferences.MixerRapid },
            { 60047, ApplianceReferences.AutoPlater },
            { 60048, ApplianceReferences.DirtyPlateStack },
            { 60049, ApplianceReferences.PotStack },
            { 60050, ApplianceReferences.ServingBoardStack },
            { 60051, ApplianceReferences.WokStack },
            { 60052, ApplianceReferences.SourceLasagneTray },
            { 60053, ApplianceReferences.ProviderTacoTray },
            { 60054, ApplianceReferences.ProviderMixingBowls },
            { 60055, ApplianceReferences.SourceBigCakeTin },
            { 60056, ApplianceReferences.SourceBrownieTray },
            { 60057, ApplianceReferences.SourceCookieTray },
            { 60058, ApplianceReferences.SourceCupcakeTray },
            { 60059, ApplianceReferences.SourceDoughnutTray },
            // ── Filler appliances ─────────────────────────────────────────────────
            { 60060, ApplianceReferences.Bin },
            { 60061, ApplianceReferences.BinCompactor },
            { 60062, ApplianceReferences.BinComposter },
            { 60063, ApplianceReferences.BinExpanded },
            { 60064, ApplianceReferences.FloorProtector },
            { 60065, ApplianceReferences.ShoeRackWellies },
            { 60066, ApplianceReferences.ShoeRackWorkBoots },
            { 60067, ApplianceReferences.ClipboardStand },
            { 60068, ApplianceReferences.IceDispenser },
            { 60069, ApplianceReferences.MilkDispenser },
            { 60070, ApplianceReferences.CoffeeMachine },
            { 60071, ApplianceReferences.MopBucket },
            { 60072, ApplianceReferences.MopBucketLasting },
            { 60073, ApplianceReferences.MopBucketFast },
            { 60074, ApplianceReferences.RobotMop },
            { 60075, ApplianceReferences.FloorBufferStation },
            { 60076, ApplianceReferences.RobotBuffer },
            { 60077, ApplianceReferences.BreadstickBox },
            { 60078, ApplianceReferences.CandleBox },
            { 60079, ApplianceReferences.NapkinBox },
            { 60080, ApplianceReferences.SharpCutlery },
            { 60081, ApplianceReferences.SpecialsMenuBox },
            { 60082, ApplianceReferences.LeftoversBagStation },
            { 60083, ApplianceReferences.SupplyCabinet },
            { 60084, ApplianceReferences.BlueprintCabinet },
            { 60085, ApplianceReferences.HostStand },
            { 60086, ApplianceReferences.FlowerPot },
            { 60087, ApplianceReferences.CoffeeTable },
            { 60088, ApplianceReferences.FoodDisplayStand },
            { 60089, ApplianceReferences.FireExtinguisherHolder },
        };

        public static readonly List<int> usefulApplianceGDOs = new List<int>
        {
            ApplianceReferences.Hob,
            ApplianceReferences.HobSafe,
            ApplianceReferences.HobDanger,
            ApplianceReferences.HobStarting,
            ApplianceReferences.Oven,
            ApplianceReferences.Microwave,
            ApplianceReferences.GasLimiter,
            ApplianceReferences.GasSafetyOverride,
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
            ApplianceReferences.TableLarge,
            ApplianceReferences.TableBar,
            ApplianceReferences.TableBasicCloth,
            ApplianceReferences.TableCheapMetal,
            ApplianceReferences.TableFancyCloth,
            ApplianceReferences.RollingPinProvider,
            ApplianceReferences.SharpKnifeProvider,
            ApplianceReferences.ScrubbingBrushProvider,
            ApplianceReferences.ShoeRackTrainers,
            ApplianceReferences.BlueprintUpgradeDesk,
            ApplianceReferences.BlueprintCopyDesk,
            ApplianceReferences.BlueprintDiscountDesk,
            ApplianceReferences.BlueprintOrderingDesk,
            ApplianceReferences.BookingDesk,
            ApplianceReferences.Dumbwaiter,
            ApplianceReferences.Teleporter,
            ApplianceReferences.OrderingTerminal,
            ApplianceReferences.OrderingTerminalSpecialOffers,
            ApplianceReferences.Belt,
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
            ApplianceReferences.DirtyPlateStack,
            ApplianceReferences.PotStack,
            ApplianceReferences.ServingBoardStack,
            ApplianceReferences.WokStack,
            ApplianceReferences.SourceLasagneTray,
            ApplianceReferences.ProviderTacoTray,
            ApplianceReferences.ProviderMixingBowls,
            ApplianceReferences.SourceBigCakeTin,
            ApplianceReferences.SourceBrownieTray,
            ApplianceReferences.SourceCookieTray,
            ApplianceReferences.SourceCupcakeTray,
            ApplianceReferences.SourceDoughnutTray,
        };

        public static readonly List<int> fillerApplianceGDOs = new List<int>
        {
            ApplianceReferences.BinStarting,
            ApplianceReferences.Bin,
            ApplianceReferences.BinCompactor,
            ApplianceReferences.BinComposter,
            ApplianceReferences.BinExpanded,
            ApplianceReferences.FloorProtector,
            ApplianceReferences.ShoeRackWellies,
            ApplianceReferences.ShoeRackWorkBoots,
            ApplianceReferences.ClipboardStand,
            ApplianceReferences.IceDispenser,
            ApplianceReferences.MilkDispenser,
            ApplianceReferences.CoffeeMachine,
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
            ApplianceReferences.BlueprintCabinet,
            ApplianceReferences.HostStand,
            ApplianceReferences.FlowerPot,
            ApplianceReferences.CoffeeTable,
            ApplianceReferences.FoodDisplayStand,
            ApplianceReferences.FireExtinguisherHolder,
            ApplianceReferences.PlateStack,
            ApplianceReferences.PlateStackStarting,
        };

        public static readonly Dictionary<string, int> usefulApplianceDictionary = new Dictionary<string, int>
        {
            { "Hob", ApplianceReferences.Hob },
            { "Hob (Safe)", ApplianceReferences.HobSafe },
            { "Hob (Danger)", ApplianceReferences.HobDanger },
            { "Oven", ApplianceReferences.Oven },
            { "Microwave", ApplianceReferences.Microwave },
            { "Gas Limiter", ApplianceReferences.GasLimiter },
            { "Gas Safety Override", ApplianceReferences.GasSafetyOverride },
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
            { "Dining Table", ApplianceReferences.TableLarge },
            { "Bar Table", ApplianceReferences.TableBar },
            { "Basic Cloth Table", ApplianceReferences.TableBasicCloth },
            { "Metal Table", ApplianceReferences.TableCheapMetal },
            { "Fancy Cloth Table", ApplianceReferences.TableFancyCloth },
            { "Rolling Pin", ApplianceReferences.RollingPinProvider },
            { "Sharp Knife", ApplianceReferences.SharpKnifeProvider },
            { "Scrubbing Brush", ApplianceReferences.ScrubbingBrushProvider },
            { "Trainers", ApplianceReferences.ShoeRackTrainers },
            { "Research Desk", ApplianceReferences.BlueprintUpgradeDesk },
            { "Copy Desk", ApplianceReferences.BlueprintCopyDesk },
            { "Discount Desk", ApplianceReferences.BlueprintDiscountDesk },
            { "Ordering Desk", ApplianceReferences.BlueprintOrderingDesk },
            { "Booking Desk", ApplianceReferences.BookingDesk },
            { "Dumbwaiter", ApplianceReferences.Dumbwaiter },
            { "Teleporter", ApplianceReferences.Teleporter },
            { "Ordering Terminal", ApplianceReferences.OrderingTerminal },
            { "Ordering Terminal (Specials)", ApplianceReferences.OrderingTerminalSpecialOffers },
            { "Conveyor Belt", ApplianceReferences.Belt },
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
            { "Dish Rack", ApplianceReferences.DirtyPlateStack },
            { "Pot Stack", ApplianceReferences.PotStack },
            { "Serving Board Stack", ApplianceReferences.ServingBoardStack },
            { "Wok Stack", ApplianceReferences.WokStack },
            { "Lasagne Tray", ApplianceReferences.SourceLasagneTray },
            { "Taco Tray", ApplianceReferences.ProviderTacoTray },
            { "Mixing Bowls", ApplianceReferences.ProviderMixingBowls },
            { "Big Cake Tin", ApplianceReferences.SourceBigCakeTin },
            { "Brownie Tray", ApplianceReferences.SourceBrownieTray },
            { "Cookie Tray", ApplianceReferences.SourceCookieTray },
            { "Cupcake Tray", ApplianceReferences.SourceCupcakeTray },
            { "Doughnut Tray", ApplianceReferences.SourceDoughnutTray },
        };

        public static readonly Dictionary<string, int> fillerApplianceDictionary = new Dictionary<string, int>
        {
            { "Bin", ApplianceReferences.Bin },
            { "Compactor Bin", ApplianceReferences.BinCompactor },
            { "Composter Bin", ApplianceReferences.BinComposter },
            { "Expanded Bin", ApplianceReferences.BinExpanded },
            { "Floor Protector", ApplianceReferences.FloorProtector },
            { "Wellies", ApplianceReferences.ShoeRackWellies },
            { "Work Boots", ApplianceReferences.ShoeRackWorkBoots },
            { "Clipboard Stand", ApplianceReferences.ClipboardStand },
            { "Ice Dispenser", ApplianceReferences.IceDispenser },
            { "Milk Dispenser", ApplianceReferences.MilkDispenser },
            { "Coffee Machine", ApplianceReferences.CoffeeMachine },
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
            { "Blueprint Cabinet", ApplianceReferences.BlueprintCabinet },
            { "Host Stand", ApplianceReferences.HostStand },
            { "Flower Pot", ApplianceReferences.FlowerPot },
            { "Coffee Table", ApplianceReferences.CoffeeTable },
            { "Food Display", ApplianceReferences.FoodDisplayStand },
            { "Fire Extinguisher Holder", ApplianceReferences.FireExtinguisherHolder },
            { "Plate Stack", ApplianceReferences.PlateStack },
            { "Starting Plate Stack", ApplianceReferences.PlateStackStarting },
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

        public static readonly Dictionary<string, int> allCustomerCards = new Dictionary<string, int>()
        {
            { "Individual dining", UnlockCardReferences.IndividualDining },
            { "Medium Groups", UnlockCardReferences.MediumGroups },
            { "Large Groups", UnlockCardReferences.LargeGroups },
            { "Flexible Dining", UnlockCardReferences.FlexibleDining },
            { "Morning Rush", UnlockCardReferences.MorningRush },
            { "Lunch Rush", UnlockCardReferences.LunchRush },
            { "Dinner Rush", UnlockCardReferences.DinnerRush },
            { "Herd Mentality", UnlockCardReferences.CustomerBursts },
            { "Advertising", UnlockCardReferences.MoreCustomers },
            { "Advertising 2", UnlockCardReferences.MoreCustomers2 },
            { "All You Can Eat", UnlockCardReferences.AllYouCanEat },
            { "Double Helpings", UnlockCardReferences.AllYouCanEatIncrease },
            { "Blindfolded Chefs", UnlockCardReferences.HiddenProcesses },
            { "Closing Time?", UnlockCardReferences.ClosingTime },
            { "Discounts", UnlockCardReferences.LessMoney },
            { "Empathy", UnlockCardReferences.HiddenPatience },
            { "Health and Safety", UnlockCardReferences.MessesSlowCustomers },
            { "High Expectations", UnlockCardReferences.PatienceDecrease },
            { "High Quality", UnlockCardReferences.SlowProcesses },
            { "High Standards", UnlockCardReferences.QuickerBurning },
            { "Instant Service", UnlockCardReferences.InstantOrders },
            { "Leisurely Eating", UnlockCardReferences.CustomersEatSlowly },
            { "Personalized Waiting", UnlockCardReferences.ChangeOrdersAfterOrdering },
            { "Picky eaters", UnlockCardReferences.HiddenOrders },
            { "Relaxed Atmosphere", UnlockCardReferences.MessyCustomers },
            { "Sedate Atmosphere", UnlockCardReferences.CustomersRequireWalking },
            { "Simplicity", UnlockCardReferences.OneUpgradePerDay },
            { "Splash Zone", UnlockCardReferences.MessRangeIncrease },
            { "Victorian Standards", UnlockCardReferences.LosePatienceInView },
        };


        public static readonly Dictionary<int, int> allDishExtras = new Dictionary<int, int>()
        {
            { 1, DishReferences.SteakSauceMushroomSauce },
            { 2, DishReferences.SteakSauceRedWineJus },
            { 3, DishReferences.SteakToppingMushroom },
            { 4, DishReferences.SteakToppingTomato },
            { 5, DishReferences.TurkeyCranberrySauce },
            { 6, DishReferences.TurkeyGravy },
            { 7, DishReferences.TurkeyStuffing },
            { 8, DishReferences.DumplingSoySauce },
            { 9, DishReferences.DumplingsSeaweed },
            { 10, DishReferences.SaladToppings },
            { 11, DishReferences.PizzaMushroom },
            { 12, DishReferences.PizzaOnion },
            { 13, DishReferences.BurgerTomatoandOnion },
            { 14, DishReferences.BurgerCheese },
            { 15, DishReferences.BurgerFreshPatties },
            { 16, DishReferences.FishExtraChoice },
            { 17, DishReferences.FishExtraChoice2 },
            { 18, DishReferences.HotdogCondimentMustard },
            { 19, DishReferences.BreakfastBeans },
            { 20, DishReferences.BreakfastExtras },
            { 21, DishReferences.BreakfastVeganExtras },
            { 22, DishReferences.StirFrySoySauce },
            { 23, -1795285445 }, // Giant Sandwiches
            { 24, -72176411 }, // Toast Sandwiches
            { 25, -469304690 }, // Sandwich - Cheese
            { 26, 525935646 }, // Sandwich - Eggs
            { 27, -778719372 }, // Sandwich - Mayo
            { 28, 368792675 }, // Sandwich - Toppers
            { 29, 641008296 }, // Club Sandwiches
            { 30, 1879652468 }, // Sundae Toppings
            { 31, -690833761 }, // Giant Sundaes
            {32, 431260200 }, // Sundae Syrups
        };

        public static readonly Dictionary<int, int> allDishSides = new Dictionary<int, int>()
        {
            { 1, DishReferences.BroccoliCheeseSoup },
            { 2, DishReferences.CarrotSoup },
            { 3, DishReferences.MeatSoup },
            { 4, DishReferences.PumpkinSoup },
            { 5, DishReferences.TomatoSoup },
            { 6, DishReferences.BreadStarter },
            { 7, DishReferences.Mandarin },
            { 8, DishReferences.PumpkinSeed },
            { 9, DishReferences.Bamboo },
            { 10, DishReferences.Broccoli },
            { 11, DishReferences.Chips },
            { 12, DishReferences.CornOnCob },
            { 13, DishReferences.MashedPotato },
            { 14, DishReferences.OnionRings },
            { 15, DishReferences.RoastPotato },
            { 16, DishReferences.PieApple },
            { 17, DishReferences.PiePumpkin },
            { 18, DishReferences.CherryPie },
            { 19, DishReferences.CheeseBoard },
            { 20, DishReferences.IceCream },
        };

        public static readonly Dictionary<int, string> trapDictionary = new Dictionary<int, string>()
        {
            { 20000, "EVERYTHING IS ON FIRE" },
            { 20001, "Super Slow" },
            { 20002, "Random Customer Card" },
            { 20003, "Patience Decrease" },
            { 20004, "More Customers" },
            { 20005, "Minimum Group Size Increase" },
            { 20006, "Maximum Group Size Increase" },
            { 20007, "Random Dish Extra" },
            { 20008, "Random Side Dish" },
            { 20009, "Tip Jar Drain" },
            { 20010, "Good Advertisement" },
            { 20011, "Card Swap" },
        };

        // Maps a dish GDO ID to the allDishExtras keys that are valid for that dish.
        public static readonly Dictionary<int, List<int>> dishExtraKeysByDish = new Dictionary<int, List<int>>()
        {
            { DishReferences.SteakBase,         new List<int> { 1, 2, 3, 4 } },
            { DishReferences.TurkeyBase,        new List<int> { 5, 6, 7 } },
            { DishReferences.Dumplings,         new List<int> { 8, 9 } },
            { DishReferences.SaladBase,         new List<int> { 10 } },
            { DishReferences.PizzaBase,         new List<int> { 11, 12 } },
            { DishReferences.BurgerBase,        new List<int> { 13, 14, 15 } },
            { DishReferences.FishBase,          new List<int> { 16, 17 } },
            { DishReferences.HotdogBase,        new List<int> { 18 } },
            { DishReferences.BreakfastBase,     new List<int> { 19, 20, 21 } },
            { DishReferences.StirFryBase,       new List<int> { 22 } },
            { -1272159363,                      new List<int> { 23, 24, 25, 26, 27, 28, 29 } }, // Sandwiches
            { 934171642,                        new List<int> { 30, 31, 32 } },                 // Sundaes
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

        // Achievement identifier string -> AP location ID
        // Identifiers confirmed from decompiled Kitchen assembly
        public static readonly Dictionary<string, int> achievementLocationIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "FIRE_RECOVERY",       140001 },
            { "FIRE_BRIGADE",       140002 },
            { "OH_NO",              140003 },
            { "CHARCOAL_FACTORY",   140004 },
            { "SAFETY_LAST",        140005 },
            { "LEARNING_BY_DOING",  140006 },
            { "PLEASE_WAIT",        140007 },
            { "FLAWLESS_TIMING",    140008 },
            { "WHAT_A_STATE",   140009 },
            { "CIRCLE_LINE",        140010 },
            { "CHEF_SCHOOL",        140011 },
            { "NEW_CHEF_PLUS",      140012 },
            { "DAY_20",         140013 },
            { "DAY_25",        140014 },
            { "DAY_30",        140015 },
            { "ANTISOCIAL",         140016 },
            { "WORK_SMART",         140017 },
        };

        // Display-name -> block index (for location IDs)
        public static readonly Dictionary<string, int> settingBlockIndex = new Dictionary<string, int>
        {
            { "Autumn", 0 },
            { "Banquet", 1 },
            { "Turbo", 2 },
            { "Witch Hut", 3 }
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

            // Base Setting uses a flat range: 160000 + day
            if (display == "Base Setting")
            {
                locId = 160000 + day;
                return true;
            }

            // Optional settings use: 161000 + (index * 1000) + day
            if (!settingBlockIndex.TryGetValue(display, out int block))
                return false;

            locId = 161000 + (block * 1000) + day;
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

        public static readonly Dictionary<string, int> dishLeaseItemIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Salad",      31101 },
            { "Steak",      31102 },
            { "Burger",     31103 },
            { "Coffee",     31104 },
            { "Pizza",      31105 },
            { "Dumplings",  31106 },
            { "Turkey",     31107 },
            { "Pie",        31108 },
            { "Cakes",      31109 },
            { "Spaghetti",  31110 },
            { "Fish",       31111 },
            { "Tacos",      31112 },
            { "Hot Dogs",   31113 },
            { "Breakfast",  31114 },
            { "Stir Fry",   31115 },
            { "Sandwiches", 31116 },
            { "Sundaes",    31117 },
        };

        /// <summary>
        /// Maps utility/filler item IDs (as sent by the apworld) to a string key.
        /// All item-ID-specific logic should look up from here rather than hardcoding numbers.
        /// </summary>
        public static readonly Dictionary<int, string> utilityItemMapping = new Dictionary<int, string>()
        {
            // ── Lease items ────────────────────────────────────────────────────────
            { 15,    "DayLease" },
            { 32000, "OvertimeDayLease" },

            // ── Economy ────────────────────────────────────────────────────────────
            { 16,    "MoneyCapIncrease" },
            { 40006, "Coin" },          // apworld v0.3+

            // ── Starting-deck management ───────────────────────────────────────────
            { 50002, "RemoveCard" },    // apworld v0.3+
            { 22,    "ShopSizeIncrease" },

            // ── Kitchen parameter fillers (cumulative deltas) ──────────────────────
            { 40001, "PatienceIncrease" },          // apworld v0.3+
            { 40002, "LessCustomers" },             // apworld v0.3+
            { 40003, "MinGroupSizeDecrease" },      // apworld v0.3+
            { 40004, "MaxGroupSizeDecrease" },      // apworld v0.3+
            { 40005, "MessReduction" },             // apworld v0.3+
            // Legacy IDs kept for backwards compatibility with older apworld seeds
            { 24,    "PatienceIncrease" },
            { 25,    "LessCustomers" },
            { 26,    "MinGroupSizeDecrease" },
            { 27,    "MaxGroupSizeDecrease" },
            { 28,    "GlobalPatienceUpgrade" },
            { 29,    "MessReduction" },
            { 23,    "ReduceGroupSize" },

            // ── Misc one-shot items ────────────────────────────────────────────────
            { 30,    "RerollToken" },
            { 31,    "ExtraLife" },
            { 100,   "DecorationUnlock" },
        };

        // Convenience look-ups so callers can match by effect name without knowing the ID
        public static int GetUtilityItemId(string key)
        {
            foreach (var kv in utilityItemMapping)
                if (kv.Value == key) return kv.Key;
            return -1;
        }

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