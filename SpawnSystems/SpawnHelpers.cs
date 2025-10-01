using Unity.Entities;
using Unity.Collections;
using UnityEngine;
using Kitchen;
using KitchenData;
using KDGameData = KitchenData.GameData;

namespace KitchenPlateupAP.Spawning
{
    internal static class SpawnHelpers
    {
        public static Vector3 ResolveSpawnPosition(EntityManager em, SpawnPositionType positionType, int inputIdentifier)
        {
            if (positionType == SpawnPositionType.Door)
            {
                // Prefer cached door position
                if (DoorPositionSystem.HasDoor)
                    return DoorPositionSystem.LastDoorPosition;

                // Fallback to GenericSystemBase via a temporary system instance (lightweight) if needed
                // (Only if we really want to attempt another fetch; otherwise skip)
                // Not creating here to avoid overhead – rely on HasDoor flag updated per frame.

                // Absolute fallback: use first player (legacy behavior)
            }

            var playerQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CPlayer>(),
                ComponentType.ReadOnly<CPosition>());

            using (var players = playerQuery.ToComponentDataArray<CPlayer>(Allocator.Temp))
            using (var positions = playerQuery.ToComponentDataArray<CPosition>(Allocator.Temp))
            {
                if (positionType == SpawnPositionType.Player)
                {
                    for (int i = 0; i < players.Length; i++)
                        if (players[i].InputSource == inputIdentifier)
                            return positions[i];
                    if (positions.Length > 0)
                        return positions[0];
                    return Vector3.zero;
                }

                // Door fallback path (when we didn’t resolve an actual door yet)
                if (positions.Length > 0)
                    return positions[0] + new Vector3(0f, 0f, -0.25f);
            }

            return Vector3.zero;
        }

        public static bool TrySpawnApplianceBlueprint(EntityManager em, int gdoId, Vector3 position, float costMode = 1f)
        {
            if (!KDGameData.Main.TryGet<Appliance>(gdoId, out var appliance, warn_if_fail: true))
                return false;

            PostHelpers.CreateOpenedLetter(new EntityContext(em), position, appliance.ID, costMode);
            return true;
        }

        public static bool TrySpawnDecor(EntityManager em, int gdoId, Vector3 position)
        {
            if (!KDGameData.Main.TryGet<Decor>(gdoId, out var decor, warn_if_fail: true))
                return false;
            if (decor.ApplicatorAppliance == null)
                return false;

            Entity entity = em.CreateEntity();
            em.AddComponentData(entity, new CCreateAppliance { ID = decor.ApplicatorAppliance.ID });
            em.AddComponentData(entity, new CPosition(position));
            em.AddComponentData(entity, new CApplyDecor { ID = decor.ID, Type = decor.Type });
            em.AddComponentData(entity, new CDrawApplianceUsing { DrawApplianceID = decor.ID });
            em.AddComponentData(entity, default(CShopEntity));
            return true;
        }
    }
}