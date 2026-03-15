using Kitchen;
using KitchenData;
using KitchenLib.Customs;
using KitchenLib.Utils;
using KitchenPlateupAP;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;

namespace PlateupAP.APPedestalChecks
{
    public class ArchipelagoBlueprint : CustomAppliance
    {
        public override string UniqueNameID => "ArchipelagoBlueprint";

        public override GameObject Prefab =>
            Mod.Bundle?.LoadAsset<GameObject>("ArchipelagoPedestal");

        public override List<(Locale, ApplianceInfo)> InfoList => new List<(Locale, ApplianceInfo)>
        {
            (Locale.English, new ApplianceInfo
            {
                Name = "Blueprint Check",
                Description = "Pay coins to send an Archipelago blueprint check."
            })
        };

        public override List<IApplianceProperty> Properties => new List<IApplianceProperty>
        {
            new CRequiresGenericInputIndicator
            {
                Message = InputIndicatorMessage.PracticeMode
            },
            new CImmovable(),
            new CFixedRotation()
        };

        public override void SetupPrefab(GameObject prefab)
        {
            foreach (var tmp in prefab.GetComponentsInChildren<TMP_Text>(includeInactive: true))
                Object.DestroyImmediate(tmp);

            foreach (var txt in prefab.GetComponentsInChildren<UnityEngine.UI.Text>(includeInactive: true))
                Object.DestroyImmediate(txt);
        }

        public static int GetGDOID()
        {
            var gdo = GDOUtils.GetCustomGameDataObject<ArchipelagoBlueprint>();
            return gdo?.GameDataObject?.ID ?? 0;
        }
    }

    /// <summary>Tags an entity as an AP blueprint-check pedestal.</summary>
    public struct CAPCheckPedestal : IComponentData
    {
        /// <summary>0-based index into the blueprint_check_ids pool.</summary>
        public int CheckIndex;
        /// <summary>Coin cost the player must pay to send this check.</summary>
        public int Cost;
        /// <summary>Physical slot position (0, 1, 2) for placement offset.</summary>
        public int SlotIndex;
    }
}