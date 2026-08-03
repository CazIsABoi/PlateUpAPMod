using HarmonyLib;
using Kitchen;
using TMPro;

namespace KitchenPlateupAP.Patches
{
    /// <summary>
    /// StartNewDay.OnUpdate is the authoritative transition from night/prep into
    /// the next day. Vanilla does not inspect SellingRequiredAppliance, so the
    /// lease gate must intercept this action rather than only populate the UI
    /// warning component.
    /// </summary>
    [HarmonyPatch(typeof(StartNewDay), "OnUpdate")]
    internal static class LeaseStartDayPatch
    {
        private static bool wasBlocking;
        private static int lastDay = -1;
        private static int lastOwned = -1;
        private static int lastRequired = -1;

        [HarmonyPrefix]
        private static bool Prefix(StartNewDay __instance)
        {
            if (!__instance.HasSingleton<SDay>())
                return true;

            int currentDay = __instance.GetSingleton<SDay>().Day;
            bool isBlocking = LeaseRequirementSystem.ShouldBlockStartDay(currentDay);
            var status = LeaseRequirementSystem.LastStatus;

            if (isBlocking
                && (!wasBlocking || currentDay != lastDay
                    || status.Owned != lastOwned || status.Required != lastRequired))
            {
                Mod.Logger.LogWarning(
                    $"[LeaseGate] Blocking day {currentDay}: owned={status.Owned}, required={status.Required}, goal={Mod.Goal}, mode={Mod.DayLeaseMode}, progressive={Mod.DayLeasesProgressive}.");
            }
            else if (!isBlocking && wasBlocking)
            {
                Mod.Logger.LogInfo(
                    $"[LeaseGate] Cleared for day {currentDay}: owned={status.Owned}, required={status.Required}, goal={Mod.Goal}, mode={Mod.DayLeaseMode}, progressive={Mod.DayLeasesProgressive}; start-day transition restored.");
            }

            wasBlocking = isBlocking;
            lastDay = currentDay;
            lastOwned = status.Owned;
            lastRequired = status.Required;

            return !isBlocking;
        }
    }

    /// <summary>
    /// Lease.cs reuses vanilla's existing warning entry to request a start-day
    /// warning view. Replace only that active lease warning's localised text;
    /// every other vanilla warning retains its normal localisation.
    /// </summary>
    [HarmonyPatch(typeof(StartDayWarningView), "UpdateData")]
    internal static class LeaseStartDayWarningViewPatch
    {
        [HarmonyPostfix]
        private static void Postfix(StartDayWarningView __instance, StartDayWarningView.ViewData view_data)
        {
            if (view_data.Warning != StartDayWarning.SellingRequiredAppliance
                || !LeaseRequirementSystem.TryGetActiveLeaseMessage(out string title, out string description))
                return;

            SetText(__instance, "NameLocalisation", title);
            SetText(__instance, "DescriptionLocalisation", description);
        }

        private static void SetText(StartDayWarningView view, string localisationField, string text)
        {
            var localisation = Traverse.Create(view).Field(localisationField).GetValue<AutoLocal>();
            if (localisation == null)
                return;

            var target = Traverse.Create(localisation).Field("_Target").GetValue<TextMeshPro>();
            if (target != null)
                target.text = text;
        }
    }
}
