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
        private bool _loggedThisFrame;

        protected override void OnUpdate()
        {
            _loggedThisFrame = false;

            if (!ArchipelagoConnectionManager.ConnectionSuccessful || ArchipelagoConnectionManager.Session == null)
            {
                LogSkip("no AP session");
                return;
            }

            if (!LockedDishes.IsLockingEnabled())
            {
                LogSkip("locking disabled");
                return;
            }

            HashSet<int> allowed = LockedDishes.GetAvailableDishes()?.ToHashSet() ?? new HashSet<int>();
            if (allowed.Count == 0)
            {
                LogSkip("allowed list empty");
                return;
            }

            EntityManager entityManager = EntityManager;
            bool destroyedAny = false;

            // Filter entities tagged explicitly as dish upgrades
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
                        destroyedAny = true;
                    }
                }
            }

            // Filter progression options that are actual dish cards (CreateDishOptions spawns these)
            EntityQuery progressionQuery = GetEntityQuery(ComponentType.ReadOnly<CProgressionOption>());
            using (NativeArray<Entity> entities = progressionQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in entities)
                {
                    if (!entityManager.Exists(entity) || !entityManager.HasComponent<CProgressionOption>(entity))
                        continue;

                    CProgressionOption option = entityManager.GetComponentData<CProgressionOption>(entity);

                    // Only act on entries that resolve to a Dish; ignore non-dish progression cards
                    if (!GameData.Main.TryGet<Dish>(option.ID, out _))
                        continue;

                    if (!allowed.Contains(option.ID))
                    {
                        entityManager.DestroyEntity(entity);
                        destroyedAny = true;
                    }
                }
            }

            if (destroyedAny)
            {
                Mod.Logger?.LogInfo($"[LockedDishes] Filtered disallowed dish options. Allowed set: {string.Join(",", allowed)}");
            }
        }

        private void LogSkip(string reason)
        {
            if (_loggedThisFrame)
                return;

            _loggedThisFrame = true;
            Mod.Logger?.LogInfo($"[LockedDishes] Skipped filtering: {reason}. enabled={LockedDishes.IsLockingEnabled()}, allowed={string.Join(",", LockedDishes.GetAvailableDishes() ?? Enumerable.Empty<int>())}");
        }
    }
}