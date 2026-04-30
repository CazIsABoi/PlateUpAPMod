using Kitchen;
using KitchenMods;
using KitchenPlateupAP.Spawning;
using PlateupAP.APPedestalChecks;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace KitchenPlateupAP
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class APCheckPedestalSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery _interactedQuery;
        private EntityQuery _allPedestalsQuery;

        private Entity _lastAffordWarningEntity = Entity.Null;
        private int _lastAffordWarningCost = -1;

        // Timestamp of the last successful purchase; prevents re-buying while
        // the player holds interact across frames.
        private float _lastPurchaseTime = -1f;
        private const float PurchaseCooldown = 0.5f;

        /// <summary>How far in front of the door (negative Z) the first pedestal spawns.</summary>
        private const float ForwardOffset = 2f;
        /// <summary>Spacing between pedestal slots along the X axis.</summary>
        private const float SlotSpacing = 1f;

        protected override void Initialise()
        {
            _interactedQuery = GetEntityQuery(
                ComponentType.ReadOnly<CAPCheckPedestal>(),
                ComponentType.ReadOnly<CPosition>(),
                ComponentType.ReadOnly<CBeingActedOnBy>());

            _allPedestalsQuery = GetEntityQuery(
                ComponentType.ReadOnly<CAPCheckPedestal>());
        }

        protected override void OnUpdate()
        {
            if (!BlueprintCheckManager.IsEnabled)
                return;

            if (!HasSingleton<SKitchenMarker>())
                return;

            if (!Require(out SMoney money))
                return;

            // Time-based cooldown: ignore interactions for PurchaseCooldown seconds
            // after each successful purchase so a held interact button can't buy
            // multiple checks in rapid succession.
            if (UnityEngine.Time.realtimeSinceStartup - _lastPurchaseTime < PurchaseCooldown)
                return;

            using var interacted = _interactedQuery.ToEntityArray(Allocator.Temp);

            if (interacted.Length == 0)
            {
                _lastAffordWarningEntity = Entity.Null;
                _lastAffordWarningCost = -1;
                return;
            }

            for (int i = 0; i < interacted.Length; i++)
            {
                Entity pedestal = interacted[i];

                if (!EntityManager.HasComponent<CBeingActedOnBy>(pedestal))
                    continue;

                var buffer = EntityManager.GetBuffer<CBeingActedOnBy>(pedestal);
                if (buffer.Length == 0)
                    continue;

                var data = EntityManager.GetComponentData<CAPCheckPedestal>(pedestal);

                if (money.Amount < data.Cost)
                {
                    if (pedestal != _lastAffordWarningEntity || data.Cost != _lastAffordWarningCost)
                    {
                        Mod.Logger.LogInfo($"[BlueprintChecks] Can't afford check {data.CheckIndex} (costs {data.Cost}, has {money.Amount}).");
                        _lastAffordWarningEntity = pedestal;
                        _lastAffordWarningCost = data.Cost;
                    }
                    continue;
                }

                _lastAffordWarningEntity = Entity.Null;
                _lastAffordWarningCost = -1;

                money.Amount -= data.Cost;
                Set(money);

                SendCheck(data.CheckIndex);
                BlueprintCheckManager.RecordPurchase(data.CheckIndex, Mod.CurrentIdentity);

                Mod.Logger.LogInfo($"[BlueprintChecks] Check {data.CheckIndex} purchased for {data.Cost} coins (slot {data.SlotIndex}).");

                // Arm the cooldown before any further work so even if UpdatePedestalInPlace
                // somehow loops back here, the guard at the top of OnUpdate blocks it.
                _lastPurchaseTime = UnityEngine.Time.realtimeSinceStartup;

                // Update the pedestal entity in-place so the player can keep
                // holding interact for the next purchase.
                if (!UpdatePedestalInPlace(pedestal, data.SlotIndex))
                {
                    EntityManager.DestroyEntity(pedestal);
                    if (PedestalCount() == 0)
                    {
                        ChatManager.AddSystemMessage("All blueprint checks completed!");
                        Mod.Logger.LogInfo("[BlueprintChecks] All checks purchased; no more pedestals to spawn.");
                    }
                }

                break;
            }
        }

        /// <summary>
        /// Updates an existing pedestal entity with the next available check.
        /// Returns false if no more checks are available.
        /// </summary>
        private bool UpdatePedestalInPlace(Entity pedestal, int slotIndex)
        {
            if (BlueprintCheckManager.AllChecksComplete)
                return false;

            int checkIndex = BlueprintCheckManager.ClaimNextIndex();
            if (checkIndex < 0)
                return false;

            int cost = BlueprintCheckManager.CostForIndex(checkIndex);

            EntityManager.SetComponentData(pedestal, new CAPCheckPedestal
            {
                CheckIndex = checkIndex,
                Cost = cost,
                SlotIndex = slotIndex
            });

            Mod.Logger.LogInfo($"[BlueprintChecks] Updated pedestal slot={slotIndex} to index={checkIndex} cost={cost}.");
            return true;
        }

        public int PedestalCount()
        {
            return _allPedestalsQuery.CalculateEntityCount();
        }

        public bool PedestalExists()
        {
            return !_allPedestalsQuery.IsEmptyIgnoreFilter;
        }

        /// <summary>
        /// Computes the world position for a given pedestal slot.
        /// Pedestals are placed in front of the door (negative Z) and spread
        /// along the X axis, offset away from the restaurant.
        /// If the door is near the left edge (x &lt;= 1), pedestals go right.
        /// Otherwise, pedestals go left.
        /// </summary>
        private static Vector3 GetSlotPosition(int slotIndex)
        {
            float totalWidth = (BlueprintCheckManager.MaxConcurrentPedestals - 1) * SlotSpacing;
            float doorX = DoorPositionSystem.LastDoorPosition.x;

            // Door near the left side: offset to the right (+X)
            // Door near the right / center: offset to the left (-X)
            float direction = doorX <= 1f ? 1f : -1f;
            float startX = direction * 3f + (direction > 0 ? 0f : -totalWidth);

            return DoorPositionSystem.LastDoorPosition
                + new Vector3(startX + slotIndex * SlotSpacing, 0f, -ForwardOffset);
        }

        public void SpawnAllPedestals()
        {
            if (!DoorPositionSystem.HasDoor)
            {
                Mod.Logger.LogWarning("[BlueprintChecks] Door position not available; deferring pedestal spawn.");
                return;
            }

            if (BlueprintCheckManager.AllChecksComplete)
                return;

            var occupiedSlots = new System.Collections.Generic.HashSet<int>();
            BlueprintCheckManager.ClearAssignments();

            using (var entities = _allPedestalsQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < entities.Length; i++)
                {
                    var data = EntityManager.GetComponentData<CAPCheckPedestal>(entities[i]);
                    occupiedSlots.Add(data.SlotIndex);
                    BlueprintCheckManager.ReserveIndex(data.CheckIndex);
                }
            }

            Mod.Logger.LogInfo($"[BlueprintChecks] Door at {DoorPositionSystem.LastDoorPosition}");

            int maxSlots = BlueprintCheckManager.MaxConcurrentPedestals;
            for (int slot = 0; slot < maxSlots; slot++)
            {
                if (occupiedSlots.Contains(slot))
                    continue;

                SpawnSinglePedestal(slot);
            }

            BlueprintCheckManager.PedestalsSpawnedThisPrep = true;
        }

        private void SpawnSinglePedestal(int slotIndex)
        {
            if (BlueprintCheckManager.AllChecksComplete)
            {
                if (PedestalCount() == 0)
                {
                    ChatManager.AddSystemMessage("All blueprint checks completed!");
                    Mod.Logger.LogInfo("[BlueprintChecks] All checks purchased; no more pedestals to spawn.");
                }
                return;
            }

            int checkIndex = BlueprintCheckManager.ClaimNextIndex();
            if (checkIndex < 0)
                return;

            int cost = BlueprintCheckManager.CostForIndex(checkIndex);
            int gdoId = ArchipelagoBlueprint.GetGDOID();

            Vector3 position = GetSlotPosition(slotIndex);

            Entity e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new CAPCheckPedestal
            {
                CheckIndex = checkIndex,
                Cost = cost,
                SlotIndex = slotIndex
            });
            EntityManager.AddComponentData(e, new CPosition(position));

            if (gdoId != 0)
            {
                EntityManager.AddComponentData(e, new CCreateAppliance { ID = gdoId });
                Mod.Logger.LogInfo($"[BlueprintChecks] Spawned pedestal slot={slotIndex} index={checkIndex} cost={cost} at {position}.");
            }
            else
            {
                Mod.Logger.LogWarning("[BlueprintChecks] GDO ID not available; pedestal has no visual.");
            }
        }

        private static void SendCheck(int index)
        {
            var session = ArchipelagoConnectionManager.Session;
            if (session == null)
                return;

            long locationId = BlueprintCheckManager.GetLocationId(index);
            if (locationId < 0)
                return;

            if (session.Locations.AllLocationsChecked.Contains(locationId))
            {
                Mod.Logger.LogInfo($"[BlueprintChecks] Check {index} (id={locationId}) already sent, skipping.");
                return;
            }

            session.Locations.CompleteLocationChecks(locationId);
            ChatManager.AddSystemMessage($"Blueprint check #{index + 1} sent!");
            Mod.Logger.LogInfo($"[BlueprintChecks] Sent location check id={locationId} for index {index}.");
        }
    }
}