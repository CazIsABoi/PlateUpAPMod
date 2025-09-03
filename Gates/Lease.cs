using System.Reflection;
using System;
using System.Linq;
using Unity.Entities;
using Kitchen;
using KitchenMods;

namespace KitchenPlateupAP
{
    [UpdateBefore(typeof(ManageStartDayWarnings))]
    public class LeaseRequirementSystem : SystemBase, IModSystem
    {
        private static bool forceRefresh = false;
        public static event Action RequestRefresh;

        public static void TriggerRefresh()
        {
            forceRefresh = true;
            RequestRefresh?.Invoke();
        }

        protected override void OnUpdate()
        {
            if (!forceRefresh)
            {
                // Only update if normal ECS update or forced
                return;
            }
            forceRefresh = false;

            // Require Archipelago connection
            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
                return;
            // Only during prep phase in an active kitchen
            if (!HasSingleton<SKitchenMarker>() || !HasSingleton<SIsNightTime>())
                return;
            // Current day
            int currentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (currentDay < 1) return;

            // Get current number of lease items from Archipelago
            int leaseCount = ArchipelagoConnectionManager.Session.Items.AllItemsReceived
                                 .Count(item => (int)item.ItemId == 15);

            // Determine franchise tier (how many franchises completed)
            int franchiseTier = 1;
            if (Mod.Instance != null)
            {
                var field = typeof(Mod).GetField("timesFranchised", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null && field.GetValue(Mod.Instance) is int val)
                    franchiseTier = val;
            }
            int franchisesDone = Math.Max(0, franchiseTier - 1);

            // Calculate required leases for this day and franchise
            int requiredLeases;
            if (currentDay > 15)
            {
                requiredLeases = 0;
            }
            else if (currentDay <= 5 && franchisesDone == 0)
            {
                requiredLeases = 0; // No leases required in first 5 days of first franchise
            }
            else if (currentDay <= 5)
            {
                requiredLeases = 1 + 3 * franchisesDone;
            }
            else if (currentDay <= 10)
            {
                requiredLeases = 2 + 3 * franchisesDone;
            }
            else
            {
                requiredLeases = 3 + 3 * franchisesDone;
            }

            // Update start-day warnings to block or allow day start
            var warnings = GetSingleton<SStartDayWarnings>();
            warnings.SellingRequiredAppliance = (requiredLeases > 0 && leaseCount < requiredLeases)
                                               ? WarningLevel.Error
                                               : WarningLevel.Safe;
            SetSingleton(warnings);
        }
    }
}
