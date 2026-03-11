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

    // Represents identity of a run / server connection used to decide reset.
    [Serializable]
    public class RunIdentity
    {
        public string Address;
        public int Port;
        public string Player;

        public override string ToString() => $"{Address}_{Port}_{Player}";
    }

    internal static class PersistenceManager
    {
        private static string RootPath => Path.Combine(Application.persistentDataPath, "PlateupAPState");

        private static string Sanitize(string value) =>
            string.Concat((value ?? "unknown").Split(Path.GetInvalidFileNameChars()));

        private static string SpeedFile(RunIdentity id) =>
            Path.Combine(RootPath, $"speed_{Sanitize(id.Address)}_{id.Port}_{Sanitize(id.Player)}.json");
        private static string PendingFile(RunIdentity id) =>
            Path.Combine(RootPath, $"pending_{Sanitize(id.Address)}_{id.Port}_{Sanitize(id.Player)}.json");
        private static string TrapCardFile(RunIdentity id) =>
            Path.Combine(RootPath, $"trapcards_{Sanitize(id.Address)}_{id.Port}_{Sanitize(id.Player)}.json");
        private static string DishDayFile(RunIdentity id) =>
            Path.Combine(RootPath, $"dishdays_{Sanitize(id.Address)}_{id.Port}_{Sanitize(id.Player)}.json");
        private static string IdentityFile => Path.Combine(RootPath, "last_identity.json");

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
                Debug.LogError("[PlateupAP][Persistence] Failed reading trap card state: " + ex.Message);
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
                Debug.LogError("[PlateupAP][Persistence] Failed saving trap card state: " + ex.Message);
            }
        }

        public static DishDayCountState LoadDishDayCounts(RunIdentity id)
        {
            EnsureDirectory();
            var path = DishDayFile(id);
            if (!File.Exists(path))
                return null;
            try
            {
                return JsonConvert.DeserializeObject<DishDayCountState>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed reading dish day counts: " + ex.Message);
                return null;
            }
        }

        public static void SaveDishDayCounts(RunIdentity id, DishDayCountState state)
        {
            EnsureDirectory();
            try
            {
                File.WriteAllText(DishDayFile(id), JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Failed saving dish day counts: " + ex.Message);
            }
        }

        public static void ResetForNewRun(RunIdentity id)
        {
            // Delete speed + pending + trap card + dish day files for new identity
            try
            {
                var speed = SpeedFile(id);
                var pending = PendingFile(id);
                var trapCards = TrapCardFile(id);
                var dishDays = DishDayFile(id);
                if (File.Exists(speed)) File.Delete(speed);
                if (File.Exists(pending)) File.Delete(pending);
                if (File.Exists(trapCards)) File.Delete(trapCards);
                if (File.Exists(dishDays)) File.Delete(dishDays);
            }
            catch (Exception ex)
            {
                Debug.LogError("[PlateupAP][Persistence] Reset failed: " + ex.Message);
            }
        }
    }
}