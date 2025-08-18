using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(ShipHUDData), menuName = "Soap/ScriptableEvents/"+ nameof(ShipHUDData))]
    public class ScriptableEventShipHUDData : ScriptableEvent<ShipHUDData>
    {
        
    }
}
