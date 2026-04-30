using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using HarmonyLib;
using Kitchen;
using KitchenLib;
using KitchenLib.Logging;
using KitchenLib.Utils;
using KitchenLib.References;
using KitchenMods;
using Newtonsoft.Json;
using KitchenData;
using KitchenPlateupAP;

namespace KitchenPlateupAP.Systems
{
    public class CardSystemController : GameSystemBase, IModSystem
    {
        private bool wasInFranchise = false;
        private bool hasSpawnedTrapCardsThisRun = false;
        private bool hasSpawnedStartingCardsThisRun = false;

        protected override void OnUpdate()
        {
            // Are we in HQ or Kitchen?
            bool inFranchise = Has<SFranchiseMarker>();
            bool inKitchen = Has<SKitchenMarker>();

            // If we leave the kitchen, let us spawn next time we come back
            if (!inKitchen)
            {
                hasSpawnedTrapCardsThisRun = false;
                hasSpawnedStartingCardsThisRun = false;
            }

            // If last frame was HQ, but now we see Kitchen, that means new run
            if (wasInFranchise && inKitchen)
            {
                // Spawn trap cards
                if (!hasSpawnedTrapCardsThisRun)
                {
                    hasSpawnedTrapCardsThisRun = true;

                    int trapCount = Mod.RandomTrapCardCount;
                    if (trapCount > 0)
                    {
                        Mod.Logger.LogInfo($"[CardSystem] Spawning {trapCount} random card(s) at new run start...");
                        SpawnTrapCardsWithPersistence(trapCount);
                    }
                    else
                    {
                        Mod.Logger.LogInfo("[CardSystem] trapCount = 0, no random trap-based cards to spawn.");
                    }
                }

                // Spawn starting cards (permanent difficulty cards from slot_data)
                if (!hasSpawnedStartingCardsThisRun)
                {
                    hasSpawnedStartingCardsThisRun = true;
                    SpawnStartingCards();
                }
            }

            // Process mid-run card removals (Remove Card item received while in kitchen)
            if (inKitchen && StartingCardManager.HasPendingRemovals)
            {
                ProcessPendingCardRemovals();
            }

            // Remember if we were in HQ for the next frame
            wasInFranchise = inFranchise;
        }

        /// <summary>
        /// Checks whether a card with the given unlock ID already exists as an active
        /// CProgressionOption.Selected or CProgressionUnlock entity.
        /// </summary>
        private bool IsCardAlreadyActive(int unlockCardId)
        {
            // Check selected progression options (cards pending application)
            EntityQuery selectedQuery = GetEntityQuery(
                ComponentType.ReadOnly<CProgressionOption>(),
                ComponentType.ReadOnly<CProgressionOption.Selected>());

            using (var entities = selectedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.Exists(entities[i]))
                        continue;
                    var option = EntityManager.GetComponentData<CProgressionOption>(entities[i]);
                    if (option.ID == unlockCardId)
                        return true;
                }
            }

            // Check already-applied progression unlocks
            EntityQuery unlockQuery = GetEntityQuery(ComponentType.ReadOnly<CProgressionUnlock>());
            using (var entities = unlockQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.Exists(entities[i]))
                        continue;
                    var unlock = EntityManager.GetComponentData<CProgressionUnlock>(entities[i]);
                    if (unlock.ID == unlockCardId)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Spawns trap cards with persistence support.
        /// On a continued run (same identity), reloads the same card GDOs.
        /// On a fresh run or different identity, picks new random cards and saves them.
        /// </summary>
        private void SpawnTrapCardsWithPersistence(int trapCount)
        {
            var identity = Mod.CachedConfig != null
                ? new RunIdentity
                {
                    Address = Mod.CachedConfig.address ?? "",
                    Port = Mod.CachedConfig.port,
                    Player = Mod.CachedConfig.playername ?? ""
                }
                : null;

            List<int> cardGDOsToSpawn = null;

            // Try loading persisted trap cards for this identity
            if (identity != null)
            {
                var persisted = PersistenceManager.LoadTrapCards(identity);
                if (persisted != null && persisted.SpawnedCardGDOs.Count > 0)
                {
                    cardGDOsToSpawn = persisted.SpawnedCardGDOs;
                    Mod.Logger.LogInfo($"[CardSystem] Loaded {cardGDOsToSpawn.Count} persisted trap card(s) for identity {identity}.");
                }
            }

            // No persisted cards — pick fresh random ones
            if (cardGDOsToSpawn == null)
            {
                cardGDOsToSpawn = new List<int>();
                for (int i = 0; i < trapCount; i++)
                {
                    int? cardId = PickRandomAvailableCard(cardGDOsToSpawn);
                    if (cardId.HasValue)
                        cardGDOsToSpawn.Add(cardId.Value);
                }

                // Persist the chosen cards
                if (identity != null && cardGDOsToSpawn.Count > 0)
                {
                    PersistenceManager.SaveTrapCards(identity, new TrapCardState { SpawnedCardGDOs = cardGDOsToSpawn });
                    Mod.Logger.LogInfo($"[CardSystem] Saved {cardGDOsToSpawn.Count} trap card(s) for identity {identity}.");
                }
            }

            // Spawn them
            foreach (int unlockCardId in cardGDOsToSpawn)
            {
                if (IsCardAlreadyActive(unlockCardId))
                {
                    Mod.Logger.LogInfo($"[CardSystem] Trap card unlockID={unlockCardId} already active, skipping.");
                    continue;
                }

                Entity e = EntityManager.CreateEntity();
                EntityManager.AddComponentData(e, new CProgressionOption
                {
                    ID = unlockCardId,
                    FromFranchise = false
                });
                EntityManager.AddComponent<CProgressionOption.Selected>(e);

                Mod.Logger.LogInfo($"[CardSystem] Trap card spawned: unlockID={unlockCardId}");
            }
        }

        /// <summary>
        /// Picks a random card GDO from the customer card dictionary that is not already
        /// active in the ECS world and not already in the exclusion list.
        /// Returns null if no cards are available.
        /// </summary>
        private int? PickRandomAvailableCard(List<int> excludeGDOs)
        {
            var dict = ProgressionMapping.customerCardDictionary;
            if (dict.Count == 0)
                return null;

            var excludeSet = new HashSet<int>(excludeGDOs);
            var available = new List<int>();
            foreach (var kv in dict)
            {
                if (!excludeSet.Contains(kv.Value) && !IsCardAlreadyActive(kv.Value))
                    available.Add(kv.Value);
            }

            if (available.Count == 0)
                return null;

            return available[UnityEngine.Random.Range(0, available.Count)];
        }

        private void SpawnStartingCards()
        {
            if (!StartingCardManager.IsEnabled)
            {
                Mod.Logger.LogInfo("[CardSystem] Starting cards disabled (mode=0 or amount=0).");
                return;
            }

            // Always re-sync the remove count from the reliable session history immediately
            // before computing the active card list. This guards against the timing window
            // where AllItemsReceived is empty when OnSuccessfulConnect runs but is fully
            // populated by the time the first run starts.
            var apSession = ArchipelagoConnectionManager.Session;
            if (apSession?.Items?.AllItemsReceived != null)
            {
                int removeCount = 0;
                foreach (var item in apSession.Items.AllItemsReceived)
                {
                    if (ProgressionMapping.utilityItemMapping.TryGetValue((int)item.ItemId, out string key) && key == "RemoveCard")
                        removeCount++;
                }
                StartingCardManager.SetRemoveCount(removeCount);
                Mod.Logger.LogInfo($"[CardSystem] Synced remove count from history: {removeCount}");
            }

            List<int> activeCards = StartingCardManager.GetActiveStartingCards();
            if (activeCards.Count == 0)
            {
                Mod.Logger.LogInfo("[CardSystem] All starting cards have been removed.");
                return;
            }

            Mod.Logger.LogInfo($"[CardSystem] Spawning {activeCards.Count} starting card(s)...");
            foreach (int unlockCardId in activeCards)
            {
                if (IsCardAlreadyActive(unlockCardId))
                {
                    Mod.Logger.LogInfo($"[CardSystem] Starting card unlockID={unlockCardId} already active, skipping.");
                    continue;
                }

                Entity e = EntityManager.CreateEntity();
                EntityManager.AddComponentData(e, new CProgressionOption
                {
                    ID = unlockCardId,
                    FromFranchise = false
                });
                EntityManager.AddComponent<CProgressionOption.Selected>(e);

                Mod.Logger.LogInfo($"[CardSystem] Starting card spawned: unlockID={unlockCardId}");
            }
        }

        private void ProcessPendingCardRemovals()
        {
            while (StartingCardManager.HasPendingRemovals)
            {
                int gdoIdToRemove = StartingCardManager.DequeuePendingRemoval();
                bool removed = false;

                // Find and destroy the CProgressionOption entity with this unlock ID
                // These are the "selected" progression options that represent active cards
                EntityQuery cardQuery = GetEntityQuery(
                    ComponentType.ReadOnly<CProgressionOption>(),
                    ComponentType.ReadOnly<CProgressionOption.Selected>());

                using (var entities = cardQuery.ToEntityArray(Allocator.Temp))
                {
                    for (int i = 0; i < entities.Length; i++)
                    {
                        Entity entity = entities[i];
                        if (!EntityManager.Exists(entity))
                            continue;

                        var option = EntityManager.GetComponentData<CProgressionOption>(entity);
                        if (option.ID == gdoIdToRemove)
                        {
                            EntityManager.DestroyEntity(entity);
                            removed = true;
                            Mod.Logger.LogInfo($"[CardSystem] Mid-run removal: destroyed card entity for unlockID={gdoIdToRemove}");
                            break; // Only remove one instance
                        }
                    }
                }

                // Also check CProgressionUnlock entities (cards that have already been applied)
                if (!removed)
                {
                    EntityQuery unlockQuery = GetEntityQuery(ComponentType.ReadOnly<CProgressionUnlock>());
                    using (var entities = unlockQuery.ToEntityArray(Allocator.Temp))
                    {
                        for (int i = 0; i < entities.Length; i++)
                        {
                            Entity entity = entities[i];
                            if (!EntityManager.Exists(entity))
                                continue;

                            var unlock = EntityManager.GetComponentData<CProgressionUnlock>(entity);
                            if (unlock.ID == gdoIdToRemove)
                            {
                                EntityManager.DestroyEntity(entity);
                                removed = true;
                                Mod.Logger.LogInfo($"[CardSystem] Mid-run removal: destroyed unlock entity for unlockID={gdoIdToRemove}");
                                break;
                            }
                        }
                    }
                }

                if (!removed)
                {
                    Mod.Logger.LogWarning($"[CardSystem] Mid-run removal: could not find entity for unlockID={gdoIdToRemove} (may not be spawned yet)");
                }
            }
        }
    }
}