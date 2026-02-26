using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(ShipHUDData), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(ShipHUDData))]
    public class ScriptableEventShipHUDData : ScriptableEvent<ShipHUDData>
    {
        
    }
}
