using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KitchenPlateupAP
{
    internal static class BlueprintCheckManager
    {
        /// <summary>How many pedestals to show at once.</summary>
        public const int MaxConcurrentPedestals = 3;

        // ── Slot-data values ─────────────────────────────────────────────────
        public static List<long> CheckIds { get; private set; } = new List<long>();
        public static int BasePrice { get; private set; } = 10;
        public static int PriceIncrease { get; private set; } = 10;
        public static bool IsEnabled => CheckIds != null && CheckIds.Count > 0;

        // ── Scouted item names ────────────────────────────────────────────────
        private static readonly Dictionary<int, string> _scoutedItemNames = new Dictionary<int, string>();
        public static bool ScoutingComplete { get; private set; } = false;
        private static bool _socketListenerRegistered = false;

        // ── Run state ─────────────────────────────────────────────────────────
        public static int TotalPurchased { get; private set; } = 0;
        private static readonly HashSet<int> _assignedIndices = new HashSet<int>();
        public static bool PedestalsSpawnedThisPrep { get; set; } = false;

        // ── Backwards compat alias ───────────────────────────────────────────
        public static int NextCheckIndex => TotalPurchased;

        // ── Initialisation ────────────────────────────────────────────────────

        public static void Configure(object rawCheckIds, int basePrice, int priceIncrease)
        {
            BasePrice = Mathf.Max(0, basePrice);
            PriceIncrease = Mathf.Max(0, priceIncrease);
            CheckIds = new List<long>();
            _scoutedItemNames.Clear();
            _assignedIndices.Clear();
            ScoutingComplete = false;
            _socketListenerRegistered = false;

            if (rawCheckIds == null)
            {
                Mod.Logger.LogInfo("[BlueprintChecks] blueprint_check_ids not present; feature disabled.");
                return;
            }

            try
            {
                var dict = JObject.Parse(rawCheckIds.ToString());
                var sorted = dict.Properties()
                    .OrderBy(p =>
                    {
                        var parts = p.Name.Split(' ');
                        return int.TryParse(parts[parts.Length - 1], out int n) ? n : 0;
                    })
                    .Select(p => (long)p.Value)
                    .ToList();

                CheckIds = sorted;
                Mod.Logger.LogInfo($"[BlueprintChecks] Configured: count={CheckIds.Count}, basePrice={BasePrice}, priceIncrease={PriceIncrease}");
            }
            catch (Exception ex)
            {
                Mod.Logger.LogWarning($"[BlueprintChecks] Failed to parse blueprint_check_ids: {ex.Message}");
            }
        }

        public static void ScoutAllLocations()
        {
            if (!IsEnabled || ScoutingComplete)
                return;

            var session = ArchipelagoConnectionManager.Session;
            if (session == null || session.Socket == null || session.Locations == null)
            {
                Mod.Logger.LogWarning("[BlueprintChecks] Cannot scout: session not ready.");
                return;
            }

            if (_socketListenerRegistered)
                return;

            int localSlot = session.ConnectionInfo.Slot;
            List<long> localCheckIds = new List<long>(CheckIds);

            session.Socket.PacketReceived += OnPacketReceived;
            _socketListenerRegistered = true;

            var scoutPacket = new LocationScoutsPacket
            {
                Locations = localCheckIds.ToArray(),
                CreateAsHint = 0
            };

            try
            {
                session.Socket.SendPacket(scoutPacket);
                Mod.Logger.LogInfo($"[BlueprintChecks] Scout packet sent for {localCheckIds.Count} locations.");
            }
            catch (Exception ex)
            {
                Mod.Logger.LogWarning($"[BlueprintChecks] Failed to send scout packet: {ex.Message}");
                session.Socket.PacketReceived -= OnPacketReceived;
                _socketListenerRegistered = false;
            }

            void OnPacketReceived(ArchipelagoPacketBase packet)
            {
                LocationInfoPacket infoPacket = packet as LocationInfoPacket;
                if (infoPacket == null)
                    return;

                session.Socket.PacketReceived -= OnPacketReceived;

                try
                {
                    foreach (NetworkItem item in infoPacket.Locations)
                    {
                        int index = localCheckIds.IndexOf(item.Location);
                        if (index < 0)
                            continue;

                        // Resolve the game name for this item's receiver so we can
                        // look up cross-game item names correctly.
                        string receiverGame = null;
                        try
                        {
                            var playerInfo = session.Players.GetPlayerInfo(item.Player);
                            if (playerInfo != null)
                                receiverGame = playerInfo.Game;
                        }
                        catch { }

                        // Try with the receiver's game first; fall back to null (local game)
                        string itemName = null;
                        if (!string.IsNullOrEmpty(receiverGame))
                            itemName = session.Items.GetItemName(item.Item, receiverGame);
                        if (string.IsNullOrEmpty(itemName))
                            itemName = session.Items.GetItemName(item.Item);
                        if (string.IsNullOrEmpty(itemName))
                            itemName = $"Item #{item.Item}";

                        string playerName = null;
                        try { playerName = session.Players.GetPlayerAlias(item.Player); } catch { }

                        _scoutedItemNames[index] = (item.Player != localSlot && !string.IsNullOrEmpty(playerName))
                            ? $"{itemName} ({playerName})"
                            : itemName;
                    }

                    ScoutingComplete = true;
                    Mod.Logger.LogInfo($"[BlueprintChecks] Scouting complete. {_scoutedItemNames.Count} item names cached.");
                }
                catch (Exception ex)
                {
                    Mod.Logger.LogWarning($"[BlueprintChecks] Error processing LocationInfo: {ex.Message}");
                    ScoutingComplete = true;
                }
            }
        }

        public static string GetItemNameForIndex(int index)
        {
            if (_scoutedItemNames.TryGetValue(index, out string name))
                return name;
            return $"Blueprint Check #{index + 1}";
        }

        public static void LoadState(BlueprintCheckState state)
        {
            TotalPurchased = state != null
                ? Mathf.Clamp(state.NextCheckIndex, 0, Math.Max(0, CheckIds.Count))
                : 0;
            _assignedIndices.Clear();
            Mod.Logger.LogInfo($"[BlueprintChecks] Loaded state: TotalPurchased={TotalPurchased}/{CheckIds.Count}");
        }

        public static void ResetForNewRun()
        {
            TotalPurchased = 0;
            _assignedIndices.Clear();
            PedestalsSpawnedThisPrep = false;
            Mod.Logger.LogInfo("[BlueprintChecks] State reset for new run.");
        }

        // ── Index assignment ──────────────────────────────────────────────────

        public static int ClaimNextIndex()
        {
            for (int i = TotalPurchased; i < CheckIds.Count; i++)
            {
                if (!_assignedIndices.Contains(i))
                {
                    _assignedIndices.Add(i);
                    return i;
                }
            }
            return -1;
        }

        public static void ReleaseIndex(int index)
        {
            _assignedIndices.Remove(index);
        }

        public static void ReserveIndex(int index)
        {
            _assignedIndices.Add(index);
        }

        public static void ClearAssignments()
        {
            _assignedIndices.Clear();
        }

        // ── Per-purchase ──────────────────────────────────────────────────────

        public static long GetLocationId(int index)
        {
            if (index < 0 || index >= CheckIds.Count)
                return -1L;
            return CheckIds[index];
        }

        public static int CostForIndex(int index) => BasePrice + index * PriceIncrease;

        public static void RecordPurchase(int purchasedIndex, RunIdentity identity)
        {
            _assignedIndices.Remove(purchasedIndex);
            TotalPurchased++;
            PersistenceManager.SaveBlueprintCheckState(identity, new BlueprintCheckState { NextCheckIndex = TotalPurchased });
            Mod.Logger.LogInfo($"[BlueprintChecks] Purchase recorded. TotalPurchased={TotalPurchased}/{CheckIds.Count}");
        }

        public static bool AllChecksComplete => TotalPurchased >= CheckIds.Count;
    }
}