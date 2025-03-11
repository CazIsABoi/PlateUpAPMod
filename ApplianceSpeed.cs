using System;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.References;
using KitchenMods;
using KitchenLib.Utils;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using KitchenPlateupAP;

namespace KitchenPlateupAP
{
    public class ApplyApplianceSpeedModifierSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ApplianceQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ApplianceQuery = GetEntityQuery(typeof(CAppliance)); // Query appliances
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>())
                return; // Only apply speed modifiers during the day

            float speedMultiplier = Mod.applianceSpeedMod; // Get the modifier from Mod.cs

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                // Ensure the appliance has the speed modifier component
                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = true, 
                        Process = 0,
                        Speed = speedMultiplier,
                        BadSpeed = 1
                    });

                    Debug.Log($"[PlateupAP] Applied appliance speed modifier {speedMultiplier} to appliance {applianceEntity.Index}");
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);

                    // Prevent infinite multiplication issue
                    if (existingMod.Speed != speedMultiplier)
                    {
                        existingMod.Speed = speedMultiplier;
                        existingMod.BadSpeed = speedMultiplier * 1.5f;
                        EntityManager.SetComponentData(applianceEntity, existingMod);

                        Debug.Log($"[PlateupAP] Updated appliance speed modifier to {speedMultiplier} for appliance {applianceEntity.Index}");
                    }
                }
            }
        }
    }
}
