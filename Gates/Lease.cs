using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Kitchen;
using KitchenMods;

namespace KitchenPlateupAP
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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

            // Feature disabled — clear gate and return
            if (!Mod.DayLeasesEnabled)
            {
                ClearGate(HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0);
                forceRefresh = false;
                return;
            }

            int currentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (currentDay < 1)
                return;

            int goal = Mod.Goal;
            int interval = Math.Max(1, Math.Min(30, Mod.DayLeaseInterval));
            int leaseMode = Mod.DayLeaseMode;    // 0 = global, 1 = dish_specific
            int overtimeDays = Mod.OvertimeDays;    // days > 15 covered by overtime leases (0 = none)
            int highestDay = (goal == 1 || goal == 2) ? Mod.HighestOverallDayReached : 0;
            int timesFranchised = Mod.Instance?.TimesFranchised ?? 1;

            var allItems = ArchipelagoConnectionManager.Session.Items.AllItemsReceived;

            bool gateActive;
            int leaseCount;
            int requiredLeases;

            // ── Branch: goal 2 + dish_specific — never gated ─────────────────
            if (leaseMode == 1 && goal == 2)
            {
                gateActive = false;
                leaseCount = 0;
                requiredLeases = 0;
            }
            // ── Branch: global mode — "Day Lease" (ID 15) gates all days ─────
            else if (leaseMode == 0)
            {
                leaseCount = allItems.Count(item => (int)item.ItemId == 15);
                requiredLeases = ComputeRequiredLeases(goal, currentDay, highestDay, timesFranchised, interval);
                gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
            }
            // ── Branch: dish_specific, goals 0/1 ─────────────────────────────
            else
            {
                if (currentDay <= 15)
                {
                    // Per-dish lease IDs from ProgressionMapping.dishLeaseItemIds
                    gateActive = false;
                    leaseCount = 0;
                    requiredLeases = 0;

                    string currentDishName = Mod.Instance?.GetDishName(Mod.Instance.ActiveDishId);

                    if (!string.IsNullOrWhiteSpace(currentDishName) && currentDishName != "Unknown"
                        && Mod.SelectedDishes.Contains(currentDishName, StringComparer.OrdinalIgnoreCase)
                        && ProgressionMapping.dishLeaseItemIds.TryGetValue(currentDishName, out int leaseItemId))
                    {
                        leaseCount = allItems.Count(item => (int)item.ItemId == leaseItemId);
                        requiredLeases = ComputeRequiredLeases(goal, currentDay, highestDay, timesFranchised, interval);
                        gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
                    }
                }
                else
                {
                    // Days > 15: "Overtime Day Lease" (ID 32000) when overtime_days > 0
                    if (overtimeDays <= 0)
                    {
                        gateActive = false;
                        leaseCount = 0;
                        requiredLeases = 0;
                    }
                    else
                    {
                        leaseCount = allItems.Count(item => (int)item.ItemId == 32000);
                        requiredLeases = ComputeRequiredOvertimeLeases(currentDay, highestDay, goal, interval);
                        gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
                    }
                }
            }

            // Days until the next lease threshold kicks in
            int daysUntilNext = 0;
            if (leaseCount >= requiredLeases)
            {
                if (leaseMode == 1 && currentDay > 15 && overtimeDays > 0)
                {
                    int overtimeHighest = Math.Max(0, highestDay - 15);
                    int nextThreshold = (requiredLeases + 1) * interval;
                    daysUntilNext = Math.Max(0, nextThreshold - overtimeHighest);
                }
                else if (goal == 0)
                {
                    int nextRequiredDay = (requiredLeases + 1) * interval;
                    daysUntilNext = Math.Max(0, nextRequiredDay - currentDay);
                }
                else
                {
                    int nextThreshold = (requiredLeases + 1) * interval;
                    daysUntilNext = Math.Max(0, nextThreshold - highestDay);
                }
            }

            if (HasSingleton<SStartDayWarnings>())
            {
                var warnings = GetSingleton<SStartDayWarnings>();
                warnings.SellingRequiredAppliance = gateActive ? WarningLevel.Error : WarningLevel.Safe;
                SetSingleton(warnings);
            }

            LastStatus = new CachedLeaseInfo
            {
                IsValid = true,
                IsPrepPhase = true,
                CurrentDay = currentDay,
                Owned = leaseCount,
                Required = requiredLeases,
                IsGateActive = gateActive,
                DaysUntilNext = daysUntilNext
            };

            forceRefresh = false;
        }

        private void ClearGate(int currentDay)
        {
            if (HasSingleton<SStartDayWarnings>())
            {
                var warnings = GetSingleton<SStartDayWarnings>();
                warnings.SellingRequiredAppliance = WarningLevel.Safe;
                SetSingleton(warnings);
            }

            LastStatus = new CachedLeaseInfo
            {
                IsValid = true,
                IsPrepPhase = true,
                CurrentDay = currentDay,
                Owned = 0,
                Required = 0,
                IsGateActive = false,
                DaysUntilNext = 0
            };
        }

        /// <summary>
        /// Required leases for global mode or dish-specific days 1–15.
        /// Goal 0: segment-based within the 15-day franchise cycle.
        /// Goals 1/2: floor(highestDayReached / interval) high-water mark —
        /// first <paramref name="interval"/> days always free.
        /// </summary>
        private static int ComputeRequiredLeases(
            int goal,
            int currentDay,
            int highestDayReached,
            int timesFranchised,
            int interval)
        {
            if (goal == 0)
            {
                if (currentDay > 15)
                    return 0;

                int segmentsPerFranchise = (int)Math.Ceiling(15.0 / interval);
                int baseOffset = segmentsPerFranchise * Math.Max(0, timesFranchised - 1);
                int withinRun = Math.Min(segmentsPerFranchise - 1, (currentDay - 1) / interval);

                if (timesFranchised == 1 && currentDay <= interval)
                    return 0;

                return baseOffset + withinRun;
            }
            else
            {
                return highestDayReached / interval;
            }
        }

        /// <summary>
        /// Required "Overtime Day Lease" (ID 32000) items for dish_specific mode
        /// on days above 15 (goals 0/1 only).
        /// floor(overtimeProgress / interval) where overtimeProgress is days past 15.
        /// </summary>
        private static int ComputeRequiredOvertimeLeases(
            int currentDay,
            int highestDayReached,
            int goal,
            int interval)
        {
            int overtimeProgress = goal == 0
                ? Math.Max(0, currentDay - 15)
                : Math.Max(0, highestDayReached - 15);

            return overtimeProgress / interval;
        }
    }
}