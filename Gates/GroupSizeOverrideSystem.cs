using Kitchen;
using KitchenMods;
using Unity.Entities;

namespace KitchenPlateupAP
{
    public class GroupSizeOverrideSystem : RestaurantSystem, IModSystem
    {
        public static int MaxGroupSizeOverride = 0;

        protected override void OnUpdate()
        {
            // Only active when connected to an Archipelago session
            if (!ArchipelagoConnectionManager.ConnectionSuccessful)
                return;

            if (!HasSingleton<SIsNightTime>())
                return;

            if (MaxGroupSizeOverride <= 0)
                return;

            if (!Require<SKitchenParameters>(out var kitchenParams))
                return;

            int cap = UnityEngine.Mathf.Clamp(MaxGroupSizeOverride, 1, 8);

            bool changed = kitchenParams.Parameters.MaximumGroupSize != cap
                        || kitchenParams.Parameters.MinimumGroupSize != 1;

            if (!changed)
                return;

            kitchenParams.Parameters.MaximumGroupSize = cap;
            kitchenParams.Parameters.MinimumGroupSize = 1;
            Set(kitchenParams);

            Mod.Logger?.LogInfo($"[GroupSize] Re-applied group size cap: min=1, max={cap}.");
        }
    }
}