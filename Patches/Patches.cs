using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using Kitchen;
using KitchenData;
using Unity.Entities;
using Unity.Collections;
using KitchenPlateupAP;
using UnityEngine;

namespace KitchenPlateupAP
{
    [HarmonyPatch(typeof(Archipelago.MultiClient.Net.Converters.ArchipelagoPacketConverter))]
    [HarmonyPatch("ReadJson")]
    public class Patch_ArchipelagoPacketConverter_ReadJson
    {
        static void Postfix(object __result)
        {
            if (__result is List<string> stringList)
            {
                stringList.RemoveAll(s => s == "Disabled");
            }
        }
    }

    [HarmonyPatch(typeof(DeterminePlayerSpeed), "OnUpdate")]
    public static class Patch_DeterminePlayerSpeed_OnUpdate
    {
        static void Postfix(DeterminePlayerSpeed __instance)
        {
            if (!__instance.HasSingleton<SIsDayTime>())
                return;

            EntityQuery query = __instance.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<CPlayer>());
            using (var playerEntities = query.ToEntityArray(Allocator.Temp))
            {
                var em = __instance.EntityManager;
                for (int i = 0; i < playerEntities.Length; i++)
                {
                    Entity playerEntity = playerEntities[i];
                    CPlayer player = em.GetComponentData<CPlayer>(playerEntity);

                    float slowMultiplier = Mod.Instance.GetPlayerSpeedMultiplier(playerEntity);
                    player.Speed *= Mod.movementSpeedMod * slowMultiplier;

                    em.SetComponentData(playerEntity, player);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ApplyItemProcesses), "Run")]
    public static class Patch_ApplyItemProcesses_Run
    {
        static void Prefix(ref float dt, Entity e, CItem item, EntityCommandBuffer ecb, ApplyItemProcesses __instance)
        {
            EntityManager em = __instance.EntityManager;

            float applianceSpeedMod = Mod.applianceSpeedMod;

            if (em.HasComponent<CItemUndergoingProcess>(e))
            {
                var itemProcess = em.GetComponentData<CItemUndergoingProcess>(e);

                if (itemProcess.Appliance != default && em.HasComponent<CAppliance>(itemProcess.Appliance))
                {
                    float speedMultiplier = applianceSpeedMod;

                    // Adjust process speed by modifying dt directly
                    dt *= speedMultiplier;
                }
            }
        }
    }
}
