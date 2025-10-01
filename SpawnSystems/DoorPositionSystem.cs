using Unity.Entities;
using UnityEngine;
using Kitchen;
using KitchenMods;

namespace KitchenPlateupAP.Spawning
{
    // Captures and caches the front door world position for other static helpers.
    public class DoorPositionSystem : GenericSystemBase, IModSystem
    {
        public static Vector3 LastDoorPosition { get; private set; } = Vector3.zero;
        public static bool HasDoor { get; private set; }

        protected override void OnUpdate()
        {
            if (GameInfo.CurrentScene != SceneType.Kitchen)
            {
                HasDoor = false;
                return;
            }

            try
            {
                // GenericSystemBase helper (may throw if underlying API changes)
                Vector3 door = GetFrontDoor(get_external_tile: true);
                if (door != default)
                {
                    LastDoorPosition = door;
                    HasDoor = true;
                }
            }
            catch
            {
                // Silently ignore; fallback used elsewhere
            }
        }
    }
}