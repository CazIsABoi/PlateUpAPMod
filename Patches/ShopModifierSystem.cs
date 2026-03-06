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

                    // Check if this is a decoration appliance
                    bool isDecoration = false;
                    if (GameData.Main.TryGet<Appliance>(option.Appliance, out var appliance))
                    {
                        isDecoration = appliance.ShoppingTags.HasFlag(ShoppingTags.Decoration);
                    }

                    if (isDecoration)
                    {
                        // Decoration unlocks disabled = let all decorations through
                        if (!Mod.DecorationUnlocksEnabled)
                            continue;

                        // Decoration unlocks enabled = filter by unlock list
                        if (!Mod.IsDecorationUnlocked(option.Appliance))
                        {
                            option.IsRemoved = true;
                            EntityManager.SetComponentData(entities[i], option);
                        }
                    }
                    else
                    {
                        // Non-decoration: filter by appliance unlock list
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
}