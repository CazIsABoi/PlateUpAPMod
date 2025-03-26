using KitchenData;
using KitchenLib.References;
using System.Collections.Generic;

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

        //Trap Dictionary
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
        };

        //ID comparison
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
            { "Stir Fry", 115 }
        };

        public static readonly Dictionary<int, string> speedUpgradeMapping = new Dictionary<int, string>()
        {
            { 10, "Speed Upgrade Player" },
            { 11, "Speed Upgrade Appliance" },
            { 12, "Speed Upgrade Cook" },
            { 13, "Speed Upgrade Chop" },
            { 14, "Speed Upgrade Clean" }
        };
    }
}