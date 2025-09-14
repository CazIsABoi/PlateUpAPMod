using HarmonyLib;
using Newtonsoft.Json;
using System;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using Kitchen;
using Archipelago.MultiClient.Net.Enums;

namespace KitchenPlateupAP
{
    // Bypass PermissionsEnumConverter when deserialising plain strings (prevents enum objects going into List<string>)
    [HarmonyPatch(typeof(Archipelago.MultiClient.Net.Converters.PermissionsEnumConverter), "ReadJson")]
    internal static class Patch_PermissionsEnumConverter_ReadJson_StringBypass
    {
        static bool Prefix(ref object __result,
                           JsonReader reader,
                           Type objectType,
                           object existingValue,
                           JsonSerializer serializer)
        {
            if (objectType == typeof(string))
            {
                // Reader may already be at a String token; otherwise just take its textual form
                if (reader.TokenType == JsonToken.String)
                {
                    __result = (string)reader.Value;
                }
                else
                {
                    // Fallback: convert current token to string safely
                    __result = reader.Value == null ? null : reader.Value.ToString();
                }
                return false; // Skip original converter
            }
            return true; // Let original handle enum targets
        }
    }

    [HarmonyPatch(typeof(DeterminePlayerSpeed), "OnUpdate")]
    public static class Patch_DeterminePlayerSpeed_OnUpdate
    {
        static void Postfix(DeterminePlayerSpeed __instance)
        {
            if (!__instance.HasSingleton<SIsDayTime>())
                return;

            var query = __instance.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<CPlayer>());
            using var playerEntities = query.ToEntityArray(Allocator.Temp);
            var em = __instance.EntityManager;

            for (int i = 0; i < playerEntities.Length; i++)
            {
                var playerEntity = playerEntities[i];
                var player = em.GetComponentData<CPlayer>(playerEntity);

                float slowMultiplier = Mod.Instance.GetPlayerSpeedMultiplier(playerEntity);
                player.Speed *= Mod.movementSpeedMod * slowMultiplier;
                em.SetComponentData(playerEntity, player);
            }
        }
    }
}