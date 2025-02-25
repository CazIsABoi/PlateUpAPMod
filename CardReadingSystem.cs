using Unity.Entities;
using System.Collections.Generic;
using Kitchen;
using KitchenLib.Logging;

namespace PlateupAP
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public class CardReadingSystem : SystemBase
    {
        // A static list to store the player's card IDs.
        public static List<int> CurrentCardIDs = new List<int>();

        protected override void OnUpdate()
        {
            // Clear the list at the start of each update.
            CurrentCardIDs.Clear();

            // Query for entities with both CardComponent and PlayerCardTag.
            Entities
                .WithAll<CardComponent, PlayerCardTag>()
                .ForEach((in CardComponent card) =>
                {
                    CurrentCardIDs.Add(card.CardId);
                }).Run();

            // Log the update using KitchenLib.Logging via the mod's logger.
            if (PlateupAP.Mod.Logger != null)
            {
                PlateupAP.Mod.Logger.LogInfo($"[CardReadingSystem] Updated: found {CurrentCardIDs.Count} card(s).");
            }
        }

        public struct CardComponent : IComponentData
        {
            public int CardId;
        }

        // Optional tag to identify player cards.
        public struct PlayerCardTag : IComponentData { }
    }
}
