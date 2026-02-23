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
        protected override void OnUpdate()
        {
            // Only clamp when we're in an actual run/kitchen
            if (!HasSingleton<SKitchenMarker>())
                return;

            // Clamp SMoney singleton
            if (Require(out SMoney money))
            {
                int cap = Mathf.Max(0, Mod.MoneyCap);
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