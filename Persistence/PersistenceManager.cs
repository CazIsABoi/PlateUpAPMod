using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace KitchenPlateupAP
{
    // Holds upgrade tier indices (already clamped to valid range).
    [Serializable]
    public class SpeedUpgradeState
    {
        public int MovementTier;
        public int ApplianceTier;
        public int CookTier;
        public int ChopTier;
        public int CleanTier;
    }

    // Holds item IDs that were received (dequeued from Archipelago) but not yet spawned in-game.
    [Serializable]
    public class PendingSpawnState
    {
        public List<int> PendingItemIDs = new List<int>();
    }

    // Holds the GDO IDs of trap cards that were spawned for the current run.
    [Serializable]
    public class TrapCardState
    {
        public List<int> SpawnedCardGDOs = new List<int>();
    }

    // Persists per-dish day counts so they survive lobby transitions and losses.
    [Serializable]
    public class DishDayCountState
    {
        // Key = dish GDO ID, Value = cumulative day count for that dish
        public Dictionary<int, int> DishDayCounts = new Dictionary<int, int>();
    }

    // Tracks which selected dishes have reached the configured goal day.
    // Dish checks only exist through Day 15, so overtime goal progress needs
    // its own persisted state instead of relying on AllLocationsChecked.
    [Serializable]
    public class DishGoalProgressState
    {
        public int DayTarget;
        public List<string> SelectedDishes = new List<string>();
        public List<string> CompletedDishes = new List<string>();
    }

    /// <summary>Persists blueprint-check progress so reconnects pick up where the player left off.</summary>
    [Serializable]
    public class BlueprintCheckState
    {
        public int NextCheckIndex;
    }

    /// <summary>
    /// Persists the set of appliance GDO IDs that currently live in the AP garage.
    /// Items are added when received from the multiworld and survive game restarts.
    /// The list is cleared when the server identity changes.
    /// Capped at 40 slots — oldest entry evicted when over capacity.
    /// </summary>
    [Serializable]
    public class GarageState
    {
        public const int MaxSlots = 40;

        /// <summary>Ordered list of appliance GDO IDs in the garage (oldest first).</summary>
        public List<int> ApplianceGDOs = new List<int>();

        /// <summary>
        /// Adds <paramref name="gdoId"/> only if it isn't already present.
        /// Evicts the oldest entry when the list exceeds <see cref="MaxSlots"/>.
        /// </summary>
        /// <returns><c>true</c> if the item was added; <c>false</c> if it was a duplicate.</returns>
        public bool TryAdd(int gdoId)
        {
            if (ApplianceGDOs.Contains(gdoId))
                return false;

            ApplianceGDOs.Add(gdoId);

            while (ApplianceGDOs.Count > MaxSlots)
                ApplianceGDOs.RemoveAt(0);

            return true;
        }
    }

    [Serializable]
    public class FranchiseProgressState
    {
        public int TimesFranchised;
        public int OverallDaysCompleted;
        public int HighestOverallDayReached;
        public int OverallStarsEarned;
    }

    internal static class PersistenceManager
    {
        private static string RootPath => Path.Combine(Application.persistentDataPath, "PlateupAPState");

        private static string Sanitize(string value) =>
            string.Concat((value ?? "unknown").Split(Path.GetInvalidFileNameChars()));

        private static string StorageKey(RunIdentity id) =>
            Sanitize(id?.PersistenceKey);

        private static string SpeedFile(RunIdentity id) =>
            Path.Combine(RootPath, $"speed_{StorageKey(id)}.json");
        private static string PendingFile(RunIdentity id) =>
            Path.Combine(RootPath, $"pending_{StorageKey(id)}.json");
        private static string TrapCardFile(RunIdentity id) =>
            Path.Combine(RootPath, $"trapcards_{StorageKey(id)}.json");
        private static string DishDayFile(RunIdentity id) =>
            Path.Combine(RootPath, $"dishdays_{StorageKey(id)}.json");
        private static string DishGoalProgressFile(RunIdentity id) =>
            Path.Combine(RootPath, $"dishgoal_{StorageKey(id)}.json");
        private static string IdentityFile => Path.Combine(RootPath, "last_identity.json");
        private static string GarageFile(RunIdentity id) =>
            Path.Combine(RootPath, $"garage_{StorageKey(id)}.json");
        private static string FranchiseProgressFile(RunIdentity id) =>
            Path.Combine(RootPath, $"{StorageKey(id)}_franchise_progress.json");

        private static RunIdentity _loadedIdentity;

        public static void EnsureDirectory()
        {
            if (!Directory.Exists(RootPath))
                Directory.CreateDirectory(RootPath);
        }

        public static RunIdentity LoadLastIdentity()
        {
            EnsureDirectory();
            if (!File.Exists(IdentityFile))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<RunIdentity>(File.ReadAllText(IdentityFile));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed to read identity: " + ex.Message);
                return null;
            }
        }

        public static void SaveIdentity(RunIdentity id)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(IdentityFile, JsonConvert.SerializeObject(id, Formatting.Indented));
                _loadedIdentity = id;
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed to save identity: " + ex.Message);
            }
        }

        public static bool ShouldResetForIdentity(RunIdentity newId)
        {
            var last = LoadLastIdentity();
            if (last == null) return false;
            // Reset if port changed OR address changed OR player changed (user specifically mentioned port; include others for safety)
            return last.Port != newId.Port || !string.Equals(last.Address, newId.Address, StringComparison.OrdinalIgnoreCase)
                   || !string.Equals(last.Player, newId.Player, StringComparison.OrdinalIgnoreCase);
        }

        public static SpeedUpgradeState LoadSpeedState(RunIdentity id)
        {
            EnsureDirectory();
            var path = SpeedFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<SpeedUpgradeState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading speed state: " + ex.Message);
                return null;
            }
        }

        public static void SaveSpeedState(RunIdentity id, SpeedUpgradeState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(SpeedFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving speed state: " + ex.Message);
            }
        }

        public static PendingSpawnState LoadPendingSpawn(RunIdentity id)
        {
            EnsureDirectory();
            var path = PendingFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<PendingSpawnState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading pending spawn: " + ex.Message);
                return null;
            }
        }

        public static void SavePendingSpawn(RunIdentity id, PendingSpawnState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(PendingFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving pending spawn: " + ex.Message);
            }
        }

        public static TrapCardState LoadTrapCards(RunIdentity id)
        {
            EnsureDirectory();
            var path = TrapCardFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<TrapCardState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading trap cards: " + ex.Message);
                return null;
            }
        }

        public static void SaveTrapCards(RunIdentity id, TrapCardState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(TrapCardFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving trap cards: " + ex.Message);
            }
        }

        public static DishGoalProgressState LoadDishGoalProgress(RunIdentity id)
        {
            EnsureDirectory();
            var path = DishGoalProgressFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<DishGoalProgressState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading dish goal progress: " + ex.Message);
                return null;
            }
        }

        public static void SaveDishGoalProgress(RunIdentity id, DishGoalProgressState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(DishGoalProgressFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving dish goal progress: " + ex.Message);
            }
        }

        public static FranchiseProgressState LoadFranchiseProgress(RunIdentity id)
        {
            EnsureDirectory();
            var path = FranchiseProgressFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<FranchiseProgressState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading franchise progress: " + ex.Message);
                return null;
            }
        }

        public static void SaveFranchiseProgress(RunIdentity id, FranchiseProgressState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(FranchiseProgressFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving franchise progress: " + ex.Message);
            }
        }

        public static void ResetForNewRun(RunIdentity id)
        {
            // Delete run-scoped state files for the new identity.
            try
            {
                var speed = SpeedFile(id);
                var pending = PendingFile(id);
                var trapCards = TrapCardFile(id);
                var dishDays = DishDayFile(id);
                var dishGoalProgress = DishGoalProgressFile(id);
                var blueprintChecks = BlueprintCheckFile(id);
                if (File.Exists(speed)) File.Delete(speed);
                if (File.Exists(pending)) File.Delete(pending);
                if (File.Exists(trapCards)) File.Delete(trapCards);
                if (File.Exists(dishDays)) File.Delete(dishDays);
                if (File.Exists(dishGoalProgress)) File.Delete(dishGoalProgress);
                if (File.Exists(blueprintChecks)) File.Delete(blueprintChecks);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Reset failed: " + ex.Message);
            }
        }

        // ── Blueprint Check State ────────────────────────────────────────────
        private static string BlueprintCheckFile(RunIdentity id) =>
            Path.Combine(RootPath, $"blueprintchecks_{StorageKey(id)}.json");

        public static BlueprintCheckState LoadBlueprintCheckState(RunIdentity id)
        {
            EnsureDirectory();
            var path = BlueprintCheckFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<BlueprintCheckState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading blueprint check state: " + ex.Message);
                return null;
            }
        }

        public static void SaveBlueprintCheckState(RunIdentity id, BlueprintCheckState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(BlueprintCheckFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving blueprint check state: " + ex.Message);
            }
        }

        // ── Garage State ─────────────────────────────────────────────────────

        public static GarageState LoadGarage(RunIdentity id)
        {
            EnsureDirectory();
            var path = GarageFile(id);
            if (!File.Exists(path))
                return new GarageState();
            try
            {
                return JsonConvert.DeserializeObject<GarageState>(File.ReadAllText(path)) ?? new GarageState();
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading garage state: " + ex.Message);
                return new GarageState();
            }
        }

        public static void SaveGarage(RunIdentity id, GarageState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(GarageFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving garage state: " + ex.Message);
            }
        }

        public static void ClearGarage(RunIdentity id)
        {
            EnsureDirectory();
            try
            {
                var path = GarageFile(id);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed clearing garage state: " + ex.Message);
            }
        }
    }
}
