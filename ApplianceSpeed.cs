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
using KitchenPlateupAP;

namespace KitchenPlateupAP
{
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
            if (!HasSingleton<SIsDayTime>()
                || Mod.applianceSpeedMode != 0)
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
                        Process = 0, 
                        Speed = speedMultiplier
                    });

                    Mod.Logger.LogInfo($"[PlateupAP] (Grouped) Applied speed {speedMultiplier} to appliance {applianceEntity.Index}");
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (existingMod.Speed != speedMultiplier)
                    {
                        existingMod.AffectsAllProcesses = true;
                        existingMod.Process = 0;
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);

                        Mod.Logger.LogInfo($"[PlateupAP] (Grouped) Updated appliance {applianceEntity.Index} speed to {speedMultiplier}");
                    }
                }
            }
        }
    }

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
            if (!HasSingleton<SIsDayTime>()
                || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.cookSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
                {
                    EntityManager.AddComponentData(applianceEntity, new CApplianceSpeedModifier
                    {
                        AffectsAllProcesses = false,
                        Process = ProcessReferences.Cook,
                        Speed = speedMultiplier
                    });
                }
                else
                {
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);
                    if (existingMod.Process == ProcessReferences.Cook
                        && existingMod.Speed != speedMultiplier)
                    {
                        existingMod.AffectsAllProcesses = false;
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);
                    }
                }
            }
        }
    }

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
            // Only run if it’s Daytime and in separate mode
            if (!HasSingleton<SIsDayTime>()
                || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.chopSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                // If there's no CApplianceSpeedModifier at all, add one for Chop
                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
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
                    // We already have a single CApplianceSpeedModifier
                    var existingMod = EntityManager.GetComponentData<CApplianceSpeedModifier>(applianceEntity);

                    // If it’s for Chop but has the wrong speed, update it
                    if (existingMod.Process == ProcessReferences.Chop
                        && existingMod.Speed != speedMultiplier)
                    {
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);
                    }
                    // else if the existing mod is for some other process,
                    // you can either remove it or skip. 
                    // For example, remove it and set Chop:
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

    // ----------------------------------
    //  ApplyKneadSpeedSystem
    // ----------------------------------
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
            // Only run if it’s Daytime and in separate mode
            if (!HasSingleton<SIsDayTime>()
                || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            // Knead uses the same multiplier as chop 
            float speedMultiplier = Mod.chopSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
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

                    if (existingMod.Process == ProcessReferences.Knead
                        && existingMod.Speed != speedMultiplier)
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
            if (!HasSingleton<SIsDayTime>()
                || Mod.applianceSpeedMode != 1)
            {
                return;
            }

            float speedMultiplier = Mod.cleanSpeedMod;

            using var appliances = ApplianceQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < appliances.Length; i++)
            {
                Entity applianceEntity = appliances[i];

                if (!EntityManager.HasComponent<CApplianceSpeedModifier>(applianceEntity))
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
                    if (existingMod.Process == ProcessReferences.Clean
                        && existingMod.Speed != speedMultiplier)
                    {
                        existingMod.AffectsAllProcesses = false;
                        existingMod.Speed = speedMultiplier;
                        EntityManager.SetComponentData(applianceEntity, existingMod);
                    }
                    else if (existingMod.Process != ProcessReferences.Clean)
                    {
                        // If you want multiple speed modifiers, add this as a second one
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
}