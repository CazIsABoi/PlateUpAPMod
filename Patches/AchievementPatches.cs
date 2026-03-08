using HarmonyLib;
using Kitchen;
using System;
using System.Reflection;

namespace KitchenPlateupAP
{
    /// <summary>
    /// Patches AchievementManager.Unlock — the single method called by ALL achievement
    /// types (both AchievementRequiresEndDay and AchievementManager subclasses) when
    /// an achievement is satisfied. This covers tutorial, kitchen, and end-of-day achievements.
    /// </summary>
    [HarmonyPatch(typeof(AchievementManager), "Unlock")]
    public static class AchievementUnlockPatch
    {
        static void Prefix(AchievementManager __instance)
        {
            if (!ArchipelagoConnectionManager.ConnectionSuccessful)
                return;

            try
            {
                PropertyInfo identifierProp = __instance.GetType().GetProperty(
                    "Identifier",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (identifierProp == null)
                    return;

                string identifier = identifierProp.GetValue(__instance) as string;
                if (string.IsNullOrEmpty(identifier))
                    return;

                Mod.Instance?.OnAchievementSatisfied(identifier);
            }
            catch (Exception ex)
            {
                Mod.Logger?.LogWarning($"[AchievementPatch] Error in Unlock patch: {ex.Message}");
            }
        }
    }
}