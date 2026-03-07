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

        public struct CachedLeaseInfo
        {
            public bool IsValid;
            public bool IsPrepPhase;
            public int CurrentDay;
            public int Owned;
            public int Required;
            public bool IsGateActive;
            public int DaysUntilNext;
        }

        public static CachedLeaseInfo LastStatus { get; private set; }

        protected override void OnUpdate()
        {
            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
                return;

            if (!HasSingleton<SKitchenMarker>() || !HasSingleton<SIsNightTime>())
                return;

            int currentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (currentDay < 1)
                return;

            int goal = Mod.Goal;
            int overallDaysCompleted = (goal == 1 || goal == 2) ? Mod.OverallDaysCompleted : 0;
            int timesFranchised = Mod.Instance?.TimesFranchised ?? 1;
            int interval = Math.Max(1, Math.Min(30, Mod.DayLeaseInterval));

            int leaseCount = ArchipelagoConnectionManager.Session.Items.AllItemsReceived
                .Count(item => (int)item.ItemId == 15);

            int requiredLeases = 0;

            if (goal == 0)
            {
                if (currentDay > 15)
                {
                    requiredLeases = 0;
                }
                else
                {
                    int segmentsPerFranchise = (int)Math.Ceiling(15.0 / interval);
                    int baseOffset = segmentsPerFranchise * Math.Max(0, timesFranchised - 1);
                    int withinRun = Math.Min(segmentsPerFranchise - 1, (currentDay - 1) / interval);

                    if (timesFranchised == 1 && currentDay <= interval)
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
                int nextOverallDay = Math.Max(1, overallDaysCompleted + 1);
                requiredLeases = (nextOverallDay - 1) / interval;
            }

            // Compute days until the next lease is required
            int daysUntilNext = 0;
            if (requiredLeases > 0 && leaseCount >= requiredLeases)
            {
                // Already have enough leases for this segment; find next threshold
                if (goal == 0)
                {
                    int nextRequiredDay = (requiredLeases + 1) * interval;
                    daysUntilNext = Math.Max(0, nextRequiredDay - currentDay);
                }
                else
                {
                    int nextThreshold = (requiredLeases + 1) * interval + 1;
                    daysUntilNext = Math.Max(0, nextThreshold - (overallDaysCompleted + 1));
                }
            }

            var warnings = GetSingleton<SStartDayWarnings>();
            warnings.SellingRequiredAppliance = (requiredLeases > 0 && leaseCount < requiredLeases)
                ? WarningLevel.Error
                : WarningLevel.Safe;
            SetSingleton(warnings);

            LastStatus = new CachedLeaseInfo
            {
                IsValid = true,
                IsPrepPhase = true,
                CurrentDay = currentDay,
                Owned = leaseCount,
                Required = requiredLeases,
                IsGateActive = requiredLeases > 0 && leaseCount < requiredLeases,
                DaysUntilNext = daysUntilNext
            };

            forceRefresh = false;
        }
    }
}