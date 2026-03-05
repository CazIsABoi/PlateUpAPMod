using Kitchen;
using Kitchen.ShopBuilder;
using KitchenData;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;

namespace KitchenPlateupAP
{
    [UpdateInGroup(typeof(ShopOptionGroup))]
    [UpdateAfter(typeof(CreateShopOptions))]
    public class ShopModifierSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery _shopOptions;

        protected override void Initialise()
        {
            base.Initialise();
            _shopOptions = GetEntityQuery(typeof(CShopBuilderOption));
        }

        protected override void OnUpdate()
        {
            // Only filter when the feature is enabled via slot data
            if (!Mod.ApplianceUnlocksEnabled)
                return;

            using (var entities = _shopOptions.ToEntityArray(Allocator.Temp))
            using (var options = _shopOptions.ToComponentDataArray<CShopBuilderOption>(Allocator.Temp))
            {
                for (int i = 0; i < options.Length; i++)
                {
                    var option = options[i];

                    // Skip already-removed options
                    if (option.IsRemoved)
                        continue;

                    // If this appliance has NOT been unlocked via Archipelago, remove it from the shop pool
                    if (!Mod.IsApplianceUnlocked(option.Appliance))
                    {
                        option.IsRemoved = true;
                        EntityManager.SetComponentData(entities[i], option);
                    }
                }
            }
        }
    }
}