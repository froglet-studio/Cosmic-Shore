using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP
{
    [CreateAssetMenu(fileName = "Event_" + nameof(ShipHUDData), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(ShipHUDData))]
    public class ScriptableEventShipHUDData : ScriptableEvent<ShipHUDData>
    {
        
    }
}
