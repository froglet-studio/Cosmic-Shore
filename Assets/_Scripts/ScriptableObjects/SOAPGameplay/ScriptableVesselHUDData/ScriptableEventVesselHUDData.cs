using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(ShipHUDData), menuName = "ScriptableObjects/Events/"+ nameof(ShipHUDData))]
    public class ScriptableEventVesselHUDData : ScriptableEvent<ShipHUDData>
    {
        
    }
}
