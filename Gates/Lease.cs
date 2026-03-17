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

            // Day leases feature is disabled: ensure gate is never active and clear status
            if (!Mod.DayLeasesEnabled)
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
                    CurrentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0,
                    Owned = 0,
                    Required = 0,
                    IsGateActive = false,
                    DaysUntilNext = 0
                };

                forceRefresh = false;
                return;
            }

            int currentDay = HasSingleton<SDay>() ? GetSingleton<SDay>().Day : 0;
            if (currentDay < 1)
                return;

            int goal = Mod.Goal;
            int interval = Math.Max(1, Math.Min(30, Mod.DayLeaseInterval));
            int leaseMode = Mod.DayLeaseMode;    // 0 = global, 1 = dish_specific
            int leaseScope = Mod.DishLeaseScope;  // 0 = all_dishes, 1 = goal_count_only

            // For goals 1 & 2 use the high-water mark (highest SDay ever completed).
            // This means losing runs early never inflate the lease requirement, and
            // replaying an early day after a loss never gates the player unnecessarily.
            int highestDay = (goal == 1 || goal == 2) ? Mod.HighestOverallDayReached : 0;
            int timesFranchised = Mod.Instance?.TimesFranchised ?? 1;

            var allItems = ArchipelagoConnectionManager.Session.Items.AllItemsReceived;

            bool gateActive;
            int leaseCount;
            int requiredLeases;

            if (leaseMode == 0)
            {
                // ── Global mode: single "Day Lease" item (ID 15) ──────────────────────
                leaseCount = allItems.Count(item => (int)item.ItemId == 15);
                requiredLeases = ComputeRequiredLeases(goal, currentDay, highestDay, timesFranchised, interval);
                gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
            }
            else
            {
                // ── Dish-specific mode: "<DishName> Day Lease" per dish ───────────────
                IReadOnlyList<string> allSelected = Mod.SelectedDishes;
                IEnumerable<string> gatedDishes = (goal == 2 && leaseScope == 1)
                    ? allSelected.Take(Mod.DishGoalCount)
                    : (IEnumerable<string>)allSelected;

                string currentDishName = Mod.Instance?.GetDishName(Mod.Instance.ActiveDishId);

                if (string.IsNullOrWhiteSpace(currentDishName) || currentDishName == "Unknown")
                {
                    // Can't identify the active dish — don't gate
                    gateActive = false;
                    leaseCount = 0;
                    requiredLeases = 0;
                }
                else if (!gatedDishes.Contains(currentDishName, StringComparer.OrdinalIgnoreCase))
                {
                    // Dish has no lease items of its own — always accessible
                    gateActive = false;
                    leaseCount = 0;
                    requiredLeases = 0;
                }
                else
                {
                    string expectedItemName = currentDishName + " Day Lease";
                    leaseCount = allItems.Count(item =>
                        string.Equals(
                            ArchipelagoConnectionManager.Session.Items.GetItemName(item.ItemId),
                            expectedItemName,
                            StringComparison.OrdinalIgnoreCase));

                    requiredLeases = ComputeRequiredLeases(goal, currentDay, highestDay, timesFranchised, interval);
                    gateActive = requiredLeases > 0 && leaseCount < requiredLeases;
                }
            }

            // Days until the next lease threshold kicks in
            int daysUntilNext = 0;
            if (leaseCount >= requiredLeases)
            {
                if (goal == 0)
                {
                    int nextRequiredDay = (requiredLeases + 1) * interval;
                    daysUntilNext = Math.Max(0, nextRequiredDay - currentDay);
                }
                else
                {
                    // Next threshold is when highestDay reaches the next interval boundary
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

        /// <summary>
        /// Returns how many lease items the player must own before they are allowed
        /// to start the current day.
        ///
        /// Goal 0 (franchise): uses <paramref name="currentDay"/> within the current
        /// 15-day run, offset by completed franchise cycles.
        ///
        /// Goals 1 &amp; 2 (day/dish): uses <paramref name="highestDayReached"/>,
        /// the high-water mark of the highest SDay ever completed.  This means:
        ///   • The first <paramref name="interval"/> days always cost 0 leases.
        ///   • Losing several runs while only reaching day 1–4 (interval=5) keeps
        ///     the requirement at 0, so fresh starts are never punished.
        ///   • The requirement only grows when the player genuinely progresses past
        ///     each interval boundary for the first time.
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
                // floor(highestDayReached / interval)
                // At highestDay=0..interval-1  → 0 leases required (full grace period)
                // At highestDay=interval        → 1 lease required
                // At highestDay=2*interval      → 2 leases required  …etc.
                return highestDayReached / interval;
            }
        }
    }
}