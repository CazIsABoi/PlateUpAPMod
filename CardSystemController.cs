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
                        for (int i = 0; i < trapCount; i++)
                        {
                            SpawnOneRandomCard();
                        }
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

        private void SpawnStartingCards()
        {
            if (!StartingCardManager.IsEnabled)
            {
                Mod.Logger.LogInfo("[CardSystem] Starting cards disabled (mode=0 or amount=0).");
                return;
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

        private void SpawnOneRandomCard()
        {
            var dict = ProgressionMapping.customerCardDictionary;
            if (dict.Count == 0)
            {
                Mod.Logger.LogWarning("[CardSystem] No customer cards in dictionary, skipping spawn...");
                return;
            }

            List<int> keys = new List<int>(dict.Keys);
            int randomIndex = UnityEngine.Random.Range(0, keys.Count);
            int randomKey = keys[randomIndex];
            int unlockCardId = dict[randomKey];

            Entity e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new CProgressionOption
            {
                ID = unlockCardId,
                FromFranchise = false
            });
            //EntityManager.AddComponent<CSkipShowingRecipe>(e);
            EntityManager.AddComponent<CProgressionOption.Selected>(e);

            Mod.Logger.LogInfo($"[CardSystem] Trap-based random card spawned: key={randomKey}, unlockID={unlockCardId}");
        }
    }
}