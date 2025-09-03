using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Collections;
using Kitchen;
using KitchenLib.Utils;

namespace KitchenPlateupAP
{
    internal static class LockedDishes
    {
        private static HashSet<int> _unlockedDishIDs = new HashSet<int>();

        public static void SetUnlockedDishes(IEnumerable<int> dishIDs)
        {
            _unlockedDishIDs = new HashSet<int>(dishIDs);
            if (Mod.Logger != null)
                Mod.Logger.LogInfo($"[LockedDishes] SetUnlockedDishes called. New set: {string.Join(", ", _unlockedDishIDs)}");
        }

        public static IEnumerable<int> GetAvailableDishes()
        {
            return _unlockedDishIDs;
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(GrantUpgrades))]
    [UpdateBefore(typeof(CreateDishOptions))]
    public class FilterDishUpgradesSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var allowed = LockedDishes.GetAvailableDishes()?.ToHashSet() ?? new HashSet<int>();
            if (allowed.Count == 0)
                return;

            var entityManager = EntityManager;
            var query = GetEntityQuery(typeof(CDishUpgrade));
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var dishUpgrade = entityManager.GetComponentData<CDishUpgrade>(entity);
                if (!allowed.Contains(dishUpgrade.DishID))
                {
                    entityManager.DestroyEntity(entity);
                }
            }
            foreach (var dishId in allowed)
            {
                if (!entities.Any(e => entityManager.GetComponentData<CDishUpgrade>(e).DishID == dishId))
                {
                    var newEntity = entityManager.CreateEntity(typeof(CDishUpgrade));
                    entityManager.SetComponentData(newEntity, new CDishUpgrade { DishID = dishId });
                }
            }
        }
    }
}