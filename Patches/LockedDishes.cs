using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Collections;
using Kitchen;
using KitchenData;

namespace KitchenPlateupAP
{
    internal static class LockedDishes
    {
        private static HashSet<int> _unlockedDishIDs = new HashSet<int>();
        private static bool _lockingEnabled = false;

        // Enables filtering behavior. Call this only after AP connects and baseline dish is set.
        public static void EnableLocking()
        {
            _lockingEnabled = true;
            Mod.Logger?.LogInfo("[LockedDishes] Locking ENABLED");
        }

        // Disables filtering behavior and clears state to avoid affecting vanilla play.
        public static void DisableLocking()
        {
            _lockingEnabled = false;
            _unlockedDishIDs.Clear();
            Mod.Logger?.LogInfo("[LockedDishes] Locking DISABLED and cleared");
        }

        public static bool IsLockingEnabled()
        {
            return _lockingEnabled;
        }

        // Replace API: resets the set (use for baseline on connect)
        public static void SetUnlockedDishes(IEnumerable<int> dishIDs)
        {
            _unlockedDishIDs = new HashSet<int>(dishIDs ?? Enumerable.Empty<int>());
            Mod.Logger?.LogInfo($"[LockedDishes] Set -> {string.Join(", ", _unlockedDishIDs)}");
        }

        // Additive API: used by item unlocks
        public static void AddUnlockedDishes(IEnumerable<int> dishIDs)
        {
            if (dishIDs == null)
                return;

            foreach (int id in dishIDs)
            {
                _unlockedDishIDs.Add(id);
            }

            Mod.Logger?.LogInfo($"[LockedDishes] Add -> {string.Join(", ", _unlockedDishIDs)}");
        }

        public static IEnumerable<int> GetAvailableDishes()
        {
            return _unlockedDishIDs;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CreateDishOptions))]
    [UpdateAfter(typeof(GrantUpgrades))]
    public class FilterDishUpgradesSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
                return;

            if (!LockedDishes.IsLockingEnabled())
                return;

            HashSet<int> allowed = LockedDishes.GetAvailableDishes()?.ToHashSet() ?? new HashSet<int>();
            if (allowed.Count == 0)
                return;

            EntityManager entityManager = EntityManager;

            // Only remove explicit dish-upgrade entities; leave progression options alone so picked cards apply
            EntityQuery dishUpgradeQuery = GetEntityQuery(ComponentType.ReadOnly<CDishUpgrade>());
            using (NativeArray<Entity> entities = dishUpgradeQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in entities)
                {
                    if (!entityManager.Exists(entity) || !entityManager.HasComponent<CDishUpgrade>(entity))
                        continue;

                    CDishUpgrade data = entityManager.GetComponentData<CDishUpgrade>(entity);
                    if (!allowed.Contains(data.DishID))
                    {
                        entityManager.DestroyEntity(entity);
                    }
                }
            }
        }
    }
}