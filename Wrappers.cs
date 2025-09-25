using UnityEngine;
using KitchenData;
using Kitchen;
using KitchenLib;
using KitchenLib.Utils;
using KitchenPlateupAP.Spawning;

namespace KitchenPlateupAP
{
    public class ApplianceWrapper : MonoBehaviour
    {
        public CCreateAppliance Appliance;
        public void InitAppliance()
        {
            Appliance = new CCreateAppliance();
        }
    }

    public class PositionWrapper : MonoBehaviour
    {
        public void SetPosition(Vector3 pos)
        {
            transform.position = pos;
        }
    }
}
