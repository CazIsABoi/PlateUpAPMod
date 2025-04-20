using System;
using System.Linq;
using Unity.Entities;
using Unity.Collections;
using Kitchen;
using KitchenData;
using KitchenMods;
using KitchenLib.Utils;
using KitchenLib.References;
using KitchenPlateupAP;

namespace KitchenPlateupAP
{
	public struct CStoredLastDay : IComponentData
	{
		public int Value;
	}

	[UpdateInGroup(typeof(SimulationSystemGroup))]
	public class SaveProgressionSystem : SystemBase, IModSystem
	{
		protected override void OnUpdate()
		{
			if (!HasSingleton<SKitchenMarker>())
				return;

			EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<CAppliance>());
			using var entities = query.ToEntityArray(Allocator.Temp);

			foreach (var entity in entities)
			{
				var appliance = EntityManager.GetComponentData<CAppliance>(entity);

				if (appliance.ID == ApplianceReferences.WallPiece)
				{
					int lastDay = AccessLastDay();

					if (!EntityManager.HasComponent<CStoredLastDay>(entity))
					{
						EntityManager.AddComponentData(entity, new CStoredLastDay { Value = lastDay });
						Mod.Logger.LogInfo($"[SaveProgressionSystem] Stored lastDay={lastDay} in WallPiece (Entity {entity.Index})");
					}
					else
					{
						var existing = EntityManager.GetComponentData<CStoredLastDay>(entity);
						if (existing.Value != lastDay)
						{
							EntityManager.SetComponentData(entity, new CStoredLastDay { Value = lastDay });
							Mod.Logger.LogInfo($"[SaveProgressionSystem] Updated lastDay to {lastDay} in WallPiece (Entity {entity.Index})");
						}
					}

					break;
				}
			}
		}

		private int AccessLastDay()
		{
			var field = typeof(Mod).GetField("lastDay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			return (int)(field?.GetValue(null) ?? 0);
		}
	}
}
