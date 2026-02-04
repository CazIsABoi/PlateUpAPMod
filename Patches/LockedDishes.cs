using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Collections;
using Kitchen;

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
            if (dishIDs == null) return;
            foreach (var id in dishIDs)
                _unlockedDishIDs.Add(id);
            Mod.Logger?.LogInfo($"[LockedDishes] Add -> {string.Join(", ", _unlockedDishIDs)}");
        }

        public static IEnumerable<int> GetAvailableDishes() => _unlockedDishIDs;
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(GrantUpgrades))]
    [UpdateBefore(typeof(CreateDishOptions))]
    public class FilterDishUpgradesSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Only filter when connected AND locking is enabled to avoid affecting normal gameplay.
            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
                return;

            if (!LockedDishes.IsLockingEnabled())
                return;

            var allowed = LockedDishes.GetAvailableDishes()?.ToHashSet() ?? new HashSet<int>();
            if (allowed.Count == 0)
            {
                // No baseline yet; don't remove vanilla content to avoid empty HQ.
                return;
            }

            var entityManager = EntityManager;
            var query = GetEntityQuery(ComponentType.ReadOnly<CDishUpgrade>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            HashSet<int> existing = new HashSet<int>();

            // Remove disallowed dish upgrades, track existing allowed ones
            foreach (var entity in entities)
            {
                if (!entityManager.Exists(entity) || !entityManager.HasComponent<CDishUpgrade>(entity))
                    continue;

                var data = entityManager.GetComponentData<CDishUpgrade>(entity);
                existing.Add(data.DishID);

                if (!allowed.Contains(data.DishID))
                {
                    entityManager.DestroyEntity(entity);
                }
            }

            // Create missing allowed dish upgrades
            foreach (var dishId in allowed)
            {
                if (!existing.Contains(dishId))
                {
                    var newEntity = entityManager.CreateEntity(typeof(CDishUpgrade));
                    entityManager.SetComponentData(newEntity, new CDishUpgrade { DishID = dishId });
                }
            }
        }
    }
}