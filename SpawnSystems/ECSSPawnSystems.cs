using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Kitchen;
using KitchenData;
using KitchenMods;

using KDGameData = KitchenData.GameData;

namespace KitchenPlateupAP.Spawning
{
    // Single authoritative enums
    public enum SpawnPositionType
    {
        Door,
        Player
    }

    public enum SpawnApplianceMode
    {
        Blueprint,
        Parcel
    }

    internal static class SpawnCosts
    {
        // 0 = free, 0.5 = half, else 1
        public static float Normalise(float raw)
        {
            if (Mathf.Approximately(raw, 0f)) return 0f;
            if (Mathf.Approximately(raw, 0.5f)) return 0.5f;
            return 1f;
        }
    }

    #region Request Data

    // Made public to satisfy exposure from public property + virtual method params
    public class SpawnRequest
    {
        public int GDOID;
        public SpawnPositionType PositionType;
        public int InputIdentifier;
        public SpawnApplianceMode ApplianceMode;
        public Type GDOType;          // typeof(Appliance) or typeof(Decor)
        public float CostMode;        // 0 | 0.5 | 1
    }

    #endregion

    #region Queue System

    public class SpawnRequestSystem : GenericSystemBase, IModSystem
    {
        private static readonly Queue<SpawnRequest> _queue = new Queue<SpawnRequest>();

        public static SpawnRequest Current { get; private set; }
        public static bool IsHandled { get; private set; } = true;

        public static void EnqueueAppliance(int gdoID, SpawnPositionType posType, int inputSrc, float costMode, SpawnApplianceMode mode = SpawnApplianceMode.Blueprint)
        {
            if (gdoID == 0) return;
            _queue.Enqueue(new SpawnRequest
            {
                GDOID = gdoID,
                PositionType = posType,
                InputIdentifier = inputSrc,
                ApplianceMode = mode,
                GDOType = typeof(Appliance),
                CostMode = costMode
            });
        }

        public static void EnqueueDecor(int gdoID, SpawnPositionType posType, int inputSrc)
        {
            if (gdoID == 0) return;
            _queue.Enqueue(new SpawnRequest
            {
                GDOID = gdoID,
                PositionType = posType,
                InputIdentifier = inputSrc,
                ApplianceMode = SpawnApplianceMode.Blueprint,
                GDOType = typeof(Decor),
                CostMode = 1f
            });
        }

        protected override void OnUpdate()
        {
            if (_queue.Count > 0)
            {
                Current = _queue.Dequeue();
                IsHandled = false;
            }
            else
            {
                Current = null;
                IsHandled = true;
            }
        }

        public static void MarkHandled() => IsHandled = true;
    }

    #endregion

    #region Base Handler

    public abstract class BaseSpawnHandlerSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery _players;

        protected override void Initialise()
        {
            base.Initialise();
            _players = GetEntityQuery(
                ComponentType.ReadOnly<CPlayer>(),
                ComponentType.ReadOnly<CPosition>());
        }

        protected abstract Type HandlesType { get; }

        protected override void OnUpdate()
        {
            var req = SpawnRequestSystem.Current;
            if (req == null || SpawnRequestSystem.IsHandled || req.GDOType != HandlesType)
                return;

            Vector3 position = ResolvePosition(req);

            try
            {
                if (req.GDOType == typeof(Appliance))
                {
                    if (KDGameData.Main.TryGet<Appliance>(req.GDOID, out var appliance, warn_if_fail: true))
                        SpawnAppliance(appliance, position, req);
                }
                else if (req.GDOType == typeof(Decor))
                {
                    if (KDGameData.Main.TryGet<Decor>(req.GDOID, out var decor, warn_if_fail: true))
                        SpawnDecor(decor, position);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Spawn] Error spawning {req.GDOType?.Name} {req.GDOID}: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                SpawnRequestSystem.MarkHandled();
            }
        }

        private Vector3 ResolvePosition(SpawnRequest req)
        {
            if (req.PositionType == SpawnPositionType.Player)
            {
                using var players = _players.ToComponentDataArray<CPlayer>(Allocator.Temp);
                using var positions = _players.ToComponentDataArray<CPosition>(Allocator.Temp);
                for (int i = 0; i < players.Length; i++)
                    if (players[i].InputSource == req.InputIdentifier)
                        return positions[i];
                if (positions.Length > 0)
                    return positions[0];
                return Vector3.zero;
            }

            // Try engine helper (may exist in your GenericSystemBase)
            try
            {
                Vector3 door = GetFrontDoor(get_external_tile: true);
                if (door != default)
                    return door;
            }
            catch { }

            // Fallback: first player with a small Z offset
            using (var positions = _players.ToComponentDataArray<CPosition>(Allocator.Temp))
            {
                if (positions.Length > 0)
                    return positions[0] + new Vector3(0f, 0f, -0.25f);
            }
            return Vector3.zero;
        }

        protected virtual void SpawnAppliance(Appliance appliance, Vector3 position, SpawnRequest req) { }
        protected virtual void SpawnDecor(Decor decor, Vector3 position) { }
    }

    #endregion

    #region Appliance Handler

    public class ApplianceSpawnSystem : BaseSpawnHandlerSystem
    {
        protected override Type HandlesType => typeof(Appliance);

        protected override void SpawnAppliance(Appliance appliance, Vector3 position, SpawnRequest req)
        {
            float costMode = SpawnCosts.Normalise(req.CostMode);

            switch (req.ApplianceMode)
            {
                case SpawnApplianceMode.Parcel:
                    PostHelpers.CreateApplianceParcel(EntityManager, position, appliance.ID);
                    break;
                case SpawnApplianceMode.Blueprint:
                default:
                    PostHelpers.CreateOpenedLetter(new EntityContext(EntityManager), position, appliance.ID, costMode);
                    break;
            }
        }
    }

    #endregion

    #region Decor Handler

    public class DecorSpawnSystem : BaseSpawnHandlerSystem
    {
        protected override Type HandlesType => typeof(Decor);

        protected override void SpawnDecor(Decor decor, Vector3 position)
        {
            if (decor.ApplicatorAppliance == null)
                return;

            Entity e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new CCreateAppliance { ID = decor.ApplicatorAppliance.ID });
            EntityManager.AddComponentData(e, new CPosition(position));
            EntityManager.AddComponentData(e, new CApplyDecor { ID = decor.ID, Type = decor.Type });
            EntityManager.AddComponentData(e, new CDrawApplianceUsing { DrawApplianceID = decor.ID });
            EntityManager.AddComponentData(e, default(CShopEntity));
        }
    }

    #endregion
}