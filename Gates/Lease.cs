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
            // Require Archipelago connection
            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
                return;

            // Only during prep phase in an active kitchen (night = prep)
            if (!HasSingleton<SKitchenMarker>() || !HasSingleton<SIsNightTime>())
                return;

            // Upcoming day
            int currentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (currentDay < 1)
                return;

            // Goal (0 = franchise_x_times, 1 = complete_x_days)
            int goal = 0;
            try
            {
                var goalField = typeof(Mod).GetField("goal", BindingFlags.NonPublic | BindingFlags.Static);
                if (goalField != null)
                    goal = (int)goalField.GetValue(null);
            }
            catch { }

            // Overall days completed (for day goal)
            int overallDaysCompleted = 0;
            if (goal == 1)
            {
                try
                {
                    var odcField = typeof(Mod).GetField("overallDaysCompleted", BindingFlags.NonPublic | BindingFlags.Static);
                    if (odcField != null)
                        overallDaysCompleted = (int)odcField.GetValue(null);
                }
                catch { }
            }

            // Active franchise index (timesFranchised starts at 1 before any completion)
            int franchiseIndex = 1;
            try
            {
                if (Mod.Instance != null)
                {
                    var tfField = typeof(Mod).GetField("timesFranchised", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (tfField != null && tfField.GetValue(Mod.Instance) is int val)
                        franchiseIndex = val;
                }
            }
            catch { }

            // Count lease items obtained
            int leaseCount = ArchipelagoConnectionManager.Session.Items.AllItemsReceived
                .Count(item => (int)item.ItemId == 15);

            // Fetch Day Lease Interval from Mod (default 5 if not available)
            int interval = 5;
            try
            {
                var intField = typeof(Mod).GetField("dayLeaseInterval", BindingFlags.NonPublic | BindingFlags.Static);
                if (intField != null)
                {
                    int read = (int)intField.GetValue(null);
                    interval = Math.Max(1, Math.Min(30, read));
                }
            }
            catch { }

            int requiredLeases = 0;

            if (goal == 0)
            {
                // Franchise goal: apply interval within a 15-day run.
                // currentDay > 15 => no further lease gating (overtime)
                if (currentDay > 15)
                {
                    requiredLeases = 0;
                }
                else
                {
                    // Number of lease segments per franchise run, with the given interval
                    int segmentsPerFranchise = (int)Math.Ceiling(15.0 / interval);

                    // Total segments completed in prior franchises
                    int baseOffset = segmentsPerFranchise * Math.Max(0, franchiseIndex - 1);

                    // Current segment (0-based) within this franchise run
                    int withinRun = Math.Min(segmentsPerFranchise - 1, (currentDay - 1) / interval);

                    // Preserve original behavior where the first segment of the very first franchise is 0
                    if (franchiseIndex == 1 && currentDay <= interval)
                    {
                        requiredLeases = 0;
                    }
                    else
                    {
                        requiredLeases = baseOffset + withinRun;
                    }
                }
            }
            else
            {
                // Day goal: cumulative requirement; every 'interval' overall days adds one required lease.
                int nextOverallDay = Math.Max(1, overallDaysCompleted + 1);
                requiredLeases = (nextOverallDay - 1) / interval;
            }

            var warnings = GetSingleton<SStartDayWarnings>();
            warnings.SellingRequiredAppliance = (requiredLeases > 0 && leaseCount < requiredLeases)
                ? WarningLevel.Error
                : WarningLevel.Safe;
            SetSingleton(warnings);

            forceRefresh = false;
        }
    }
}