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
            ApplianceQuery = GetEntityQuery(typeof(CAppliance));
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>())
                return;

            float speedMultiplier = Mod.applianceSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = true,
                        Process = 0,
                        Speed = Mathf.Clamp(speedMultiplier, 0.1f, 2f), 
                        BadSpeed = 1  // Keeping BadSpeed constant
                    });

                    Mod.Logger.LogInfo($"[PlateupAP] Applied initial appliance speed multiplier {speedMultiplier} to appliance {applianceEntity.Index}");
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);

                    if (existingMod.Speed != speedMultiplier)
                    {
                        existingMod.Speed = Mathf.Clamp(speedMultiplier, 0.1f, 2f);  // Ensure valid range
                        EntityManager.SetComponentData(applianceEntity, existingMod);

                        Mod.Logger.LogInfo($"[PlateupAP] Updated appliance {applianceEntity.Index} speed to {speedMultiplier}, keeping BadSpeed {existingMod.BadSpeed}");
                    }
                }
            }
        }
    }
}
