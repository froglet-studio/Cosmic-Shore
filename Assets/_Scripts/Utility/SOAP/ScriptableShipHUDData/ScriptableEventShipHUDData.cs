using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "Event_" + nameof(ShipHUDData), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(ShipHUDData))]
    public class ScriptableEventShipHUDData : ScriptableEvent<ShipHUDData>
    {
        
    }
}
