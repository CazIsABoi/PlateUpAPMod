using HarmonyLib;
using Kitchen;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Archipelago.MultiClient.Net.Enums;

namespace KitchenPlateupAP
{
    [HarmonyPatch(typeof(Archipelago.MultiClient.Net.Converters.PermissionsEnumConverter), "ReadJson")]
    internal static class Patch_PermissionsEnumConverter_ReadJson
    {
        // Prefix returns false when we fully handle deserialization ourselves.
        static bool Prefix(
            ref object __result,
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            try
            {
                // Let original run for raw Permissions or nullable Permissions or collections of Permissions.
                if (objectType == typeof(Permissions) ||
                    objectType == typeof(Permissions?) ||
                    IsPermissionsCollection(objectType))
                {
                    return true; // allow original method
                }

                // Snapshot token so we don't leave reader mid-stream
                JToken token = JToken.Load(reader);

                // If target is List<string> (or IList<string> / string[]), we sanitize "Disabled".
                if (IsStringList(objectType))
                {
                    var strings = token.Type == JTokenType.Array
                        ? token.Children().Select(t => (string)t).Where(s => s != "Disabled").ToList()
                        : new List<string>();

                    if (IsConcreteList(objectType))
                    {
                        __result = strings;
                    }
                    else if (objectType.IsArray)
                    {
                        __result = strings.ToArray();
                    }
                    else
                    {
                        // Try to create instance of requested collection and add
                        var listInstance = (IList)Activator.CreateInstance(objectType);
                        foreach (var s in strings) listInstance.Add(s);
                        __result = listInstance;
                    }

                    Debug.Log($"[PlateupAP][PermPatch] Sanitized string list ({strings.Count} entries).");
                    return false;
                }

                // For any other non-Permissions target: convert using an isolated serializer
                var cleanSettings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter>() // empty -> avoids invoking the same converter recursively
                };
                var cleanSerializer = JsonSerializer.Create(cleanSettings);
                __result = token.ToObject(objectType, cleanSerializer);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlateupAP][PermPatch] Fallback failed for {objectType}: {ex.Message}");
                // Let original attempt if we failed
                return true;
            }
        }

        private static bool IsPermissionsCollection(Type t)
        {
            if (!typeof(IEnumerable).IsAssignableFrom(t))
                return false;

            if (t.IsArray)
                return t.GetElementType() == typeof(Permissions);

            if (t.IsGenericType)
            {
                var arg = t.GetGenericArguments().FirstOrDefault();
                return arg == typeof(Permissions);
            }
            return false;
        }

        private static bool IsStringList(Type t)
        {
            if (t == typeof(List<string>) || t == typeof(IList<string>) || t == typeof(IEnumerable<string>))
                return true;
            if (t.IsArray && t.GetElementType() == typeof(string))
                return true;
            if (t.IsGenericType)
            {
                var ga = t.GetGenericArguments();
                return ga.Length == 1 && ga[0] == typeof(string) &&
                       (typeof(IList<>).MakeGenericType(typeof(string)).IsAssignableFrom(t) ||
                        typeof(IEnumerable<>).MakeGenericType(typeof(string)).IsAssignableFrom(t));
            }
            return false;
        }

        private static bool IsConcreteList(Type t)
        {
            return t == typeof(List<string>) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)
                                                 && t.GetGenericArguments()[0] == typeof(string));
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