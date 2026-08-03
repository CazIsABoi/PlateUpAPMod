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

            if (!Mod.SlotDataLoaded)
            {
                ClearGate(HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0);
                forceRefresh = false;
                return;
            }

            if (!HasSingleton<SKitchenMarker>() || !HasSingleton<SIsNightTime>())
                return;

            // Feature disabled — clear gate and return
            if (!Mod.DayLeasesEnabled || Mod.DebugLeaseGateDisabled)
            {
                ClearGate(HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0);
                forceRefresh = false;
                return;
            }

            int currentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (currentDay < 1)
                return;

            LastStatus = BuildStatus(currentDay);
            SetStartDayWarning(LastStatus.IsGateActive);
            forceRefresh = false;
        }

        /// <summary>
        /// Called from the StartNewDay Harmony boundary. Recompute against the live
        /// AP receipt pool so a received lease clears an already-displayed gate
        /// before the next SimulationSystemGroup update.
        /// </summary>
        public static bool ShouldBlockStartDay(int currentDay)
        {
            if (!Mod.SlotDataLoaded || !Mod.DayLeasesEnabled || Mod.DebugLeaseGateDisabled)
                return false;

            if (!ArchipelagoConnectionManager.ConnectionSuccessful
                || ArchipelagoConnectionManager.Session == null
                || ArchipelagoConnectionManager.Session.Items == null)
                return false;

            if (currentDay < 1)
                return false;

            LastStatus = BuildStatus(currentDay);
            forceRefresh = false;
            return LastStatus.IsGateActive;
        }

        /// <summary>
        /// Supplies the start-day warning view with lease-specific copy while the
        /// lease requirement is active. This uses the live receipt pool for the
        /// same immediate-refresh behaviour as the start-day boundary.
        /// </summary>
        public static bool TryGetActiveLeaseMessage(out string title, out string description)
        {
            title = null;
            description = null;

            if (!Mod.SlotDataLoaded || !Mod.DayLeasesEnabled || Mod.DebugLeaseGateDisabled
                || !ArchipelagoConnectionManager.ConnectionSuccessful
                || ArchipelagoConnectionManager.Session == null
                || ArchipelagoConnectionManager.Session.Items == null
                || !LastStatus.IsValid
                || !LastStatus.IsPrepPhase)
                return false;

            LastStatus = BuildStatus(LastStatus.CurrentDay);
            forceRefresh = false;
            if (!LastStatus.IsGateActive)
                return false;

            string leaseName = Mod.DayLeaseMode == 1
                ? (LastStatus.CurrentDay > 15 && Mod.OvertimeDays > 0 ? "Overtime Day Lease" : "Dish Day Lease")
                : "Day Lease";

            title = leaseName + " required";
            description = $"Receive {leaseName} from Archipelago ({LastStatus.Owned}/{LastStatus.Required} received).";
            return true;
        }

        private static CachedLeaseInfo BuildStatus(int currentDay)
        {
            int goal = Mod.Goal;
            int interval = Math.Max(1, Math.Min(30, Mod.DayLeaseInterval));
            int leaseMode = Mod.DayLeaseMode;    // 0 = global, 1 = dish_specific
            int overtimeDays = Mod.OvertimeDays;
            int highestDay = (goal == 1 || goal == 2) ? Mod.HighestOverallDayReached : 0;
            int timesFranchised = Mod.Instance?.TimesFranchised ?? 0;

            var allItems = ArchipelagoConnectionManager.Session.Items.AllItemsReceived;

            bool gateActive;
            int leaseCount;
            int requiredLeases;

            // ── Branch: global mode — "Day Lease" (ID 15) gates all days ─────
            if (leaseMode == 0)
            {
                leaseCount = allItems.Count(item => (int)item.ItemId == 15);
                requiredLeases = ComputeRequiredLeases(goal, currentDay, highestDay, timesFranchised, interval);
                gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
            }
            // ── Branch: dish_specific — the active dish's lease gates its run ─
            else
            {
                if (currentDay <= 15)
                {
                    // Per-dish lease IDs from ProgressionMapping.dishLeaseItemIds.
                    // This also applies to goal 2; dish_lease_scope determines
                    // whether the current dish participates in that goal's gate.
                    gateActive = false;
                    leaseCount = 0;
                    requiredLeases = 0;

                    string currentDishName = Mod.Instance?.GetDishName(Mod.Instance.ActiveDishId);

                    if (!string.IsNullOrWhiteSpace(currentDishName) && currentDishName != "Unknown"
                        && ProgressionMapping.dishLeaseItemIds.TryGetValue(currentDishName, out int leaseItemId))
                    {
                        bool dishIsInScope = IsDishInLeaseScope(goal, currentDishName);

                        if (dishIsInScope)
                        {
                            leaseCount = allItems.Count(item => (int)item.ItemId == leaseItemId);
                            requiredLeases = ComputeRequiredLeases(goal, currentDay, highestDay, timesFranchised, interval, true);
                            gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
                        }
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
                else
                {
                    int leaseDay = goal == 0 && leaseMode == 0
                        ? Math.Max(0, timesFranchised) * 15 + currentDay
                        : goal == 1
                            ? Math.Max(currentDay, highestDay + 1)
                            : currentDay;
                    int nextRequiredDay = Mod.DayLeasesProgressive
                        ? requiredLeases * interval + 1
                        : (requiredLeases + 1) * interval + 1;
                    daysUntilNext = Math.Max(0, nextRequiredDay - leaseDay);
                }
            }

            return new CachedLeaseInfo
            {
                IsValid = true,
                IsPrepPhase = true,
                CurrentDay = currentDay,
                Owned = leaseCount,
                Required = requiredLeases,
                IsGateActive = gateActive,
                DaysUntilNext = daysUntilNext
            };
        }

        private void SetStartDayWarning(bool gateActive)
        {
            if (!HasSingleton<SStartDayWarnings>())
                return;

            var warnings = GetSingleton<SStartDayWarnings>();
            warnings.SellingRequiredAppliance = gateActive ? WarningLevel.Error : WarningLevel.Safe;
            SetSingleton(warnings);
        }

        private void ClearGate(int currentDay)
        {
            SetStartDayWarning(false);

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
        /// Goal 0 / global: treats franchise days as one global sequence.
        /// Goal 0 / dish-specific: interval-based scaling — standard leases make
        ///   the first interval free, while progressive leases include it.
        /// Goal 1: uses the next AP progress day, preserving high-water progress
        ///   across failed or restarted restaurants.
        /// Goal 2: an active dish's current-run day determines its requirement;
        ///   standard leases use floor((day - 1) / interval), while progressive
        ///   leases use ceil(day / interval).
        /// </summary>
        private static int ComputeRequiredLeases(
            int goal,
            int currentDay,
            int highestDayReached,
            int timesFranchised,
            int interval,
            bool isDishSpecific = false)
        {
            int raw;
            if (goal == 0)
            {
                if (currentDay > 15)
                    return 0;

                if (isDishSpecific)
                {
                    raw = ComputeLeaseBlocksForDay(currentDay, interval);
                }
                else
                {
                    int globalDay = Math.Max(0, timesFranchised) * 15 + currentDay;
                    raw = ComputeLeaseBlocksForDay(globalDay, interval);
                }
            }
            else if (goal == 1)
            {
                int effectiveNextDay = Math.Max(currentDay, highestDayReached + 1);
                raw = ComputeLeaseBlocksForDay(effectiveNextDay, interval);
            }
            else
            {
                // Goal 2 gates the active dish's current run.
                raw = ComputeLeaseBlocksForDay(currentDay, interval);
            }

            // Global and dish-specific pools report separate maximum counts.
            int maximumAvailable = isDishSpecific ? Mod.MaxDishDayLeases : Mod.MaxDayLeases;
            return Math.Min(raw, maximumAvailable);
        }

        private static int ComputeLeaseBlocksForDay(int day, int interval)
        {
            if (day < 1)
                return 0;

            // Progressive: ceil(day / interval), so even the first block needs a lease.
            // Standard: floor((day - 1) / interval), so the first block is free.
            return Mod.DayLeasesProgressive
                ? (day + interval - 1) / interval
                : (day - 1) / interval;
        }

        private static bool IsDishInLeaseScope(int goal, string dishName)
        {
            // goal_count_only applies only to goal 2. Goals 0 and 1 always give
            // every participating dish its own lease set.
            if (goal != 2 || Mod.DishLeaseScope == 0)
                return true;

            return Mod.SelectedDishes
                .Take(Math.Max(0, Mod.DishGoalCount))
                .Contains(dishName, StringComparer.OrdinalIgnoreCase);
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
