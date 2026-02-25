using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptableClassType
{
    [CreateAssetMenu(fileName = "Event_" + nameof(VesselClassType), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(VesselClassType))]
    public class ScriptableEventShipClassType : ScriptableEvent<VesselClassType>
    {
    
    }
}
