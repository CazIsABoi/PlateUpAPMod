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

        protected override void OnUpdate()
        {
            // Are we in HQ or Kitchen?
            bool inFranchise = Has<SFranchiseMarker>();
            bool inKitchen = Has<SKitchenMarker>();

            // If we leave the kitchen, let us spawn next time we come back
            if (!inKitchen)
            {
                hasSpawnedTrapCardsThisRun = false;
            }

            // If last frame was HQ, but now we see Kitchen, that means new run
            if (wasInFranchise && inKitchen && !hasSpawnedTrapCardsThisRun)
            {
                hasSpawnedTrapCardsThisRun = true;

                // Spawn an amount of random cards = RandomTrapCardCount
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

            // Remember if we were in HQ for the next frame
            wasInFranchise = inFranchise;
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
