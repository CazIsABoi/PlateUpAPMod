using Kitchen;
using KitchenMods;
using Unity.Entities;
using UnityEngine;

namespace KitchenPlateupAP
{
    // Runs during simulation; clamps money to a configurable cap
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class MoneyCapSystem : GenericSystemBase, IModSystem
    {
        // Local cap so we don't depend on Mod.MoneyCap
        private const int Cap = 500; // adjust as desired

        protected override void OnUpdate()
        {
            // Only clamp when we're in an actual run/kitchen
            if (!HasSingleton<SKitchenMarker>())
                return;

            // Clamp SMoney singleton
            if (Require(out SMoney money))
            {
                int cap = Mathf.Max(0, Cap);
                if (money.Amount > cap)
                {
                    int before = money.Amount;
                    money.Amount = cap;
                    Set(money);
                    Mod.Logger?.LogInfo($"[MoneyCap] Clamped money from {before} to {cap}");
                }
            }
        }
    }
}