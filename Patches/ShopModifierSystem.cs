using Kitchen;
using Kitchen.ShopBuilder;
using KitchenData;
using KitchenMods;
using Unity.Collections;
using Unity.Entities;

namespace KitchenPlateupAP
{
    // Runs after CreateShopOptions (which has OrderFirst in ShopOptionGroup) and removes
    // CShopBuilderOption entries for appliances/decorations the player hasn't unlocked yet.
    // ShopPreFilterSystem was removed because its [UpdateBefore(CreateShopOptions)] was
    // silently ignored (CreateShopOptions uses OrderFirst/OrderLast precedence), making it
    // run at an undefined position with no effect.
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
            if (!Mod.ApplianceUnlocksEnabled && !Mod.DecorationUnlocksEnabled)
                return;

            using var entities = _shopOptions.ToEntityArray(Allocator.Temp);
            using var options = _shopOptions.ToComponentDataArray<CShopBuilderOption>(Allocator.Temp);

            for (int i = 0; i < options.Length; i++)
            {
                var option = options[i];
                if (option.IsRemoved)
                    continue;

                bool isDecoration = false;
                if (GameData.Main.TryGet<Appliance>(option.Appliance, out var appliance))
                    isDecoration = appliance.ShoppingTags.HasFlag(ShoppingTags.Decoration);

                bool shouldRemove = isDecoration
                    ? (Mod.DecorationUnlocksEnabled && !Mod.IsDecorationUnlocked(option.Appliance))
                    : (Mod.ApplianceUnlocksEnabled && !Mod.IsApplianceUnlocked(option.Appliance));

                if (shouldRemove)
                {
                    option.IsRemoved = true;
                    EntityManager.SetComponentData(entities[i], option);
                }
            }
        }
    }
}