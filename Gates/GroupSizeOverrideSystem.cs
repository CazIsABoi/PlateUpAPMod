using Kitchen;
using KitchenData;
using KitchenMods;
using Unity.Entities;

namespace KitchenPlateupAP
{
    /// <summary>
    /// Forces SKitchenParameters.MaximumGroupSize (and MinimumGroupSize) to the AP
    /// group size cap at the start of each prep phase.
    /// 0 = disabled (no override — vanilla group sizes apply).
    /// </summary>
    public class GroupSizeOverrideSystem : RestaurantSystem, IModSystem
    {
        public static int MaxGroupSizeOverride = 0;

        private bool _appliedThisPrep = false;

        protected override void OnUpdate()
        {
            bool isPrepPhase = HasSingleton<SIsNightTime>();
            bool isPrepStart = HasSingleton<SIsNightFirstUpdate>();

            // Reset flag when leaving prep (daytime)
            if (!isPrepPhase)
            {
                _appliedThisPrep = false;
                return;
            }

            // Wait one frame after prep start so AutoGrowGroupSizes runs first,
            // then apply once and hold for the rest of the prep phase.
            if (isPrepStart || _appliedThisPrep)
                return;

            if (MaxGroupSizeOverride <= 0)
            {
                _appliedThisPrep = true;
                return;
            }

            if (!Require<SKitchenParameters>(out var kitchenParams))
                return;

            int cap = UnityEngine.Mathf.Clamp(MaxGroupSizeOverride, 1, 8);

            // Force the group size to the cap value — do not just clamp downward.
            // Vanilla default max is 2; we need to set it to whatever the AP cap is.
            bool changed = kitchenParams.Parameters.MaximumGroupSize != cap
                        || kitchenParams.Parameters.MinimumGroupSize != 1;

            kitchenParams.Parameters.MaximumGroupSize = cap;
            kitchenParams.Parameters.MinimumGroupSize = 1;

            if (changed)
            {
                Set(kitchenParams);
                Mod.Logger?.LogInfo($"[GroupSize] Set SKitchenParameters group size: min=1, max={cap}.");
            }

            _appliedThisPrep = true;
        }
    }
}