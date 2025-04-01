using System;
using Kitchen;
using KitchenData;
using KitchenLib;
using KitchenLib.References;
using KitchenMods;
using KitchenLib.Utils;
using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace KitchenPlateupAP
{
    // GROUPED mode system
    public class ApplyApplianceSpeedModifierSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ApplianceQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ApplianceQuery = GetEntityQuery(typeof(CAppliance));
        }

        protected override void OnUpdate()
        {
            // Only run if it's daytime and applianceSpeedMode == 0 (grouped)
            if (!HasSingleton<SIsDayTime>() || Mod.applianceSpeedMode != 0)
            {
                return;
            }

            float speedMultiplier = Mod.applianceSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = true,
                        Process = 0, // irrelevant when AffectsAllProcesses = true
                        Speed = speedMultiplier
                    });

                    Mod.Logger.LogInfo($"[PlateupAP] (Grouped) Applied speed {speedMultiplier} to appliance {applianceEntity.Index}");
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    // if it's already a grouped mod, just update it
                    if (existingMod.AffectsAllProcesses)
                    {
                        if (existingMod.Speed != speedMultiplier)
                        {
                            existingMod.Speed = speedMultiplier;
                            EntityManager.SetComponentData(applianceEntity, existingMod);

                            Mod.Logger.LogInfo($"[PlateupAP] (Grouped) Updated appliance {applianceEntity.Index} speed to {speedMultiplier}");
                        }
                    }
                    else
                    {
                        // if the existing mod is from separate mode, remove it and add the grouped one
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                        {
                            AffectsAllProcesses = true,
                            Process = 0,
                            Speed = speedMultiplier
                        });
                        Mod.Logger.LogInfo($"[PlateupAP] (Grouped) Replaced separate mod with grouped speed {speedMultiplier} on appliance {applianceEntity.Index}");
                    }
                }
            }
        }
    }

    // SEPARATE mode: Cook
    public class ApplyCookSpeedSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ApplianceQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ApplianceQuery = GetEntityQuery(typeof(CAppliance));
        }

        protected override void OnUpdate()
        {
            // Only run if it's daytime and separate mode
            if (!HasSingleton<SIsDayTime>() || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.cookSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                // 1) If there's a leftover grouped mod, remove it
                if (EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    var leftover = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (leftover.AffectsAllProcesses)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        Mod.Logger.LogInfo($"[CookSpeed] Removed leftover grouped speed mod on appliance {applianceEntity.Index}");
                    }
                }

                // 2) Look up the GDO for this appliance
                var cA = EntityManager.GetComponentData<CAppliance>(applianceEntity);
                Appliance gdo = GameData.Main.Get<Appliance>(cA.ID);
                if (gdo == null || gdo.Processes == null)
                    continue;

                // 3) Check if GDO supports Cook
                bool canCook = false;
                foreach (var processSetting in gdo.Processes)
                {
                    if ((int)processSetting.Process == ProcessReferences.Cook)
                    {
                        canCook = true;
                        break;
                    }
                }
                if (!canCook)
                    continue;

                // 4) Apply / update the speed modifier
                bool hasSpeedMod = EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity);
                if (!hasSpeedMod)
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = false,
                        Process = ProcessReferences.Cook,
                        Speed = speedMultiplier
                    });
                    Mod.Logger.LogInfo($"[CookSpeed] Added cook speed x{speedMultiplier} to appliance {applianceEntity.Index}");
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    // if it's the Cook mod, update it if needed
                    if (existingMod.Process == ProcessReferences.Cook && existingMod.Speed != speedMultiplier)
                    {
                        float oldSpeed = existingMod.Speed;
                        existingMod.Speed = speedMultiplier;
                        existingMod.AffectsAllProcesses = false;
                        EntityManager.SetComponentData(applianceEntity, existingMod);

                        Mod.Logger.LogInfo($"[CookSpeed] Updated cook speed from {oldSpeed} to {speedMultiplier} on {applianceEntity.Index}");
                    }
                }
            }
        }
    }

    // SEPARATE mode: Chop
    public class ApplyChopSpeedSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ApplianceQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ApplianceQuery = GetEntityQuery(typeof(CAppliance));
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>() || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.chopSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                // remove leftover grouped mod
                if (EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    var leftover = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (leftover.AffectsAllProcesses)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        Mod.Logger.LogInfo($"[ChopSpeed] Removed leftover grouped speed mod on appliance {applianceEntity.Index}");
                    }
                }

                var cA = EntityManager.GetComponentData<CAppliance>(applianceEntity);
                Appliance gdo = GameData.Main.Get<Appliance>(cA.ID);
                if (gdo == null || gdo.Processes == null)
                    continue;

                bool canChop = false;
                foreach (var processSetting in gdo.Processes)
                {
                    if ((int)processSetting.Process == ProcessReferences.Chop)
                    {
                        canChop = true;
                        break;
                    }
                }
                if (!canChop)
                    continue;

                bool hasSpeedMod = EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity);
                if (!hasSpeedMod)
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = false,
                        Process = ProcessReferences.Chop,
                        Speed = speedMultiplier
                    });
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (existingMod.Process == ProcessReferences.Chop && existingMod.Speed != speedMultiplier)
                    {
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);
                    }
                    else if (existingMod.Process != ProcessReferences.Chop)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                        {
                            AffectsAllProcesses = false,
                            Process = ProcessReferences.Chop,
                            Speed = speedMultiplier
                        });
                    }
                }
            }
        }
    }

    // SEPARATE mode: Knead
    public class ApplyKneadSpeedSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ApplianceQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ApplianceQuery = GetEntityQuery(typeof(CAppliance));
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>() || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.chopSpeedMod; // uses same multiplier as Chop

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                // remove leftover grouped mod
                if (EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    var leftover = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (leftover.AffectsAllProcesses)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        Mod.Logger.LogInfo($"[KneadSpeed] Removed leftover grouped speed mod on appliance {applianceEntity.Index}");
                    }
                }

                var cA = EntityManager.GetComponentData<CAppliance>(applianceEntity);
                Appliance gdo = GameData.Main.Get<Appliance>(cA.ID);
                if (gdo == null || gdo.Processes == null)
                    continue;

                bool canKnead = false;
                foreach (var processSetting in gdo.Processes)
                {
                    if ((int)processSetting.Process == ProcessReferences.Knead)
                    {
                        canKnead = true;
                        break;
                    }
                }
                if (!canKnead)
                    continue;

                bool hasSpeedMod = EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity);
                if (!hasSpeedMod)
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = false,
                        Process = ProcessReferences.Knead,
                        Speed = speedMultiplier
                    });
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (existingMod.Process == ProcessReferences.Knead && existingMod.Speed != speedMultiplier)
                    {
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);
                    }
                    else if (existingMod.Process != ProcessReferences.Knead)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                        {
                            AffectsAllProcesses = false,
                            Process = ProcessReferences.Knead,
                            Speed = speedMultiplier
                        });
                    }
                }
            }
        }
    }

    // SEPARATE mode: Clean
    public class ApplyCleanSpeedSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ApplianceQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ApplianceQuery = GetEntityQuery(typeof(CAppliance));
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>() || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.cleanSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                // remove leftover grouped mod
                if (EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    var leftover = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (leftover.AffectsAllProcesses)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        Mod.Logger.LogInfo($"[CleanSpeed] Removed leftover grouped speed mod on appliance {applianceEntity.Index}");
                    }
                }

                var cA = EntityManager.GetComponentData<CAppliance>(applianceEntity);
                Appliance gdo = GameData.Main.Get<Appliance>(cA.ID);
                if (gdo == null || gdo.Processes == null)
                    continue;

                bool canClean = false;
                foreach (var processSetting in gdo.Processes)
                {
                    if ((int)processSetting.Process == ProcessReferences.Clean)
                    {
                        canClean = true;
                        break;
                    }
                }
                if (!canClean)
                    continue;

                bool hasSpeedMod = EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity);
                if (!hasSpeedMod)
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = false,
                        Process = ProcessReferences.Clean,
                        Speed = speedMultiplier
                    });
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (existingMod.Process == ProcessReferences.Clean && existingMod.Speed != speedMultiplier)
                    {
                        existingMod.AffectsAllProcesses = false;
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);
                    }
                    else if (existingMod.Process != ProcessReferences.Clean)
                    {
                        EntityManager.RemoveComponent<CApplianceSpeedModifier>(applianceEntity);
                        EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                        {
                            AffectsAllProcesses = false,
                            Process = ProcessReferences.Clean,
                            Speed = speedMultiplier
                        });
                    }
                }
            }
        }
    }

    // SEPARATE mode: background check system
    public class UpdateSeparateApplianceSpeedModifiersSystem : GenericSystemBase, IModSystem
    {
        private EntityQuery ProcessingQuery;

        protected override void Initialise()
        {
            base.Initialise();
            ProcessingQuery = GetEntityQuery(ComponentType.ReadOnly<CItemUndergoingProcess>());
        }

        protected override void OnUpdate()
        {
            if (!HasSingleton<SIsDayTime>() || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            Mod.Logger.LogInfo("[UpdateSeparateApplianceSpeedModifiersSystem] OnUpdate -> Checking processed appliances...");

            var entityManager = EntityManager;
            using var processingEntities = ProcessingQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < processingEntities.Length; i++)
            {
                Entity itemEntity = processingEntities[i];
                var itemProcess = entityManager.GetComponentData<CItemUndergoingProcess>(itemEntity);

                Entity applianceEntity = itemProcess.Appliance;
                if (!entityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                    continue;

                // Only one speed mod is stored at a time, so see which process it references
                var speedMod = entityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                float newMultiplier;

                if (speedMod.Process == ProcessReferences.Cook)
                {
                    newMultiplier = Mod.cookSpeedMod;
                }
                else if (speedMod.Process == ProcessReferences.Chop
                         || speedMod.Process == ProcessReferences.Knead)
                {
                    newMultiplier = Mod.chopSpeedMod;
                }
                else if (speedMod.Process == ProcessReferences.Clean)
                {
                    newMultiplier = Mod.cleanSpeedMod;
                }
                else
                {
                    // If it's leftover grouped or unknown
                    newMultiplier = speedMod.Speed;
                }

                if (Math.Abs(speedMod.Speed - newMultiplier) > 0.0001f)
                {
                    float old = speedMod.Speed;
                    speedMod.Speed = newMultiplier;
                    entityManager.SetComponentData(applianceEntity, speedMod);

                    Mod.Logger.LogInfo($"[UpdateSeparateApplianceSpeedModifiersSystem] Updated process={speedMod.Process} on entity {applianceEntity.Index} from {old} to {newMultiplier}");
                }
            }
        }
    }
}
