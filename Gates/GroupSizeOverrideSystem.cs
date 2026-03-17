using Kitchen;
using KitchenMods;
using Unity.Entities;

namespace KitchenPlateupAP
{
    /// <summary>
    /// Forces SKitchenParameters.MaximumGroupSize (and MinimumGroupSize) to the AP
    /// group size cap every prep frame, overriding vanilla AutoGrowGroupSizes.
    /// 0 = disabled (no override — vanilla group sizes apply).
    /// </summary>
    public class GroupSizeOverrideSystem : RestaurantSystem, IModSystem
    {
        public static int MaxGroupSizeOverride = 0;

        protected override void OnUpdate()
        {
            // Only active during prep (night) phase
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