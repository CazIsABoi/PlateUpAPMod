using HarmonyLib;
using Kitchen;
using PlateupAP.APPedestalChecks;
using TMPro;
using Unity.Entities;

namespace KitchenPlateupAP
{
    [HarmonyPatch(typeof(ManageGenericPromptIndicators), "CreateIndicator")]
    internal static class Patch_ManageGenericPromptIndicators_CreateIndicator
    {
        static void Postfix(Entity __result, Entity source, ManageGenericPromptIndicators __instance)
        {
            if (__result == default)
                return;

            var em = __instance.EntityManager;
            if (!em.HasComponent<CAPCheckPedestal>(source))
                return;

            var data = em.GetComponentData<CAPCheckPedestal>(source);
            string itemName = BlueprintCheckManager.GetItemNameForIndex(data.CheckIndex);
            APIndicatorTracker.Register(__result, data.Cost, itemName);
        }
    }

    [HarmonyPatch(typeof(ManageGenericPromptIndicators), "DestroyIndicator")]
    internal static class Patch_ManageGenericPromptIndicators_DestroyIndicator
    {
        static void Prefix(Entity indicator)
        {
            if (indicator != default)
                APIndicatorTracker.Remove(indicator.Index);
        }
    }

    /// <summary>
    /// Tracks which indicator entities belong to AP pedestals.
    /// With multiple pedestals, each indicator maps to its own cost/item.
    /// </summary>
    internal static class APIndicatorTracker
    {
        private static readonly System.Collections.Generic.Dictionary<int, (int cost, string itemName)> _map
            = new System.Collections.Generic.Dictionary<int, (int, string)>();

        public static void Register(Entity indicator, int cost, string itemName)
        {
            _map[indicator.Index] = (cost, itemName);
        }

        public static void Remove(int entityIndex)
        {
            _map.Remove(entityIndex);
        }

        public static bool TryGet(int entityIndex, out int cost, out string itemName)
        {
            if (_map.TryGetValue(entityIndex, out var val))
            {
                cost = val.cost;
                itemName = val.itemName;
                return true;
            }
            cost = 0;
            itemName = null;
            return false;
        }

        /// <summary>Returns the latest tracked pedestal info, or null if none.</summary>
        public static (int cost, string itemName)? GetCurrent()
        {
            (int cost, string itemName)? latest = null;
            foreach (var kvp in _map)
                latest = kvp.Value;
            return latest;
        }

        public static void Clear() => _map.Clear();
    }

    [HarmonyPatch(typeof(GenericPromptIndicatorView), "UpdateData")]
    internal static class Patch_GenericPromptIndicatorView_UpdateData
    {
        static void Postfix(GenericPromptIndicatorView __instance, GenericPromptIndicatorView.ViewData data)
        {
            if (data.Message != InputIndicatorMessage.PracticeMode)
                return;

            // With multiple pedestals, GetCurrent() still works because each indicator
            // is created/destroyed independently. The view's UpdateData is called per-view,
            // so whichever indicator entity's data was last written is the one being shown.
            var current = APIndicatorTracker.GetCurrent();
            if (current == null)
                return;

            var (cost, itemName) = current.Value;

            var activeText = Traverse.Create(__instance).Field("ActiveText").GetValue<TextMeshPro>();
            var additionalText = Traverse.Create(__instance).Field("AdditionalText").GetValue<TextMeshPro>();

            if (activeText != null)
                activeText.text = itemName;

            if (additionalText != null)
            {
                additionalText.enabled = true;
                additionalText.text = $"Send Check (${cost})";
            }
        }
    }
}