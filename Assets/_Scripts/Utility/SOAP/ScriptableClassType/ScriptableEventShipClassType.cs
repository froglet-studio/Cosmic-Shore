using UnityEngine;
using Obvious.Soap;
using CosmicShore.Models.Enums;

namespace CosmicShore.Utility.SOAP
{
    [CreateAssetMenu(fileName = "Event_" + nameof(VesselClassType), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(VesselClassType))]
    public class ScriptableEventShipClassType : ScriptableEvent<VesselClassType>
    {
    
    }
}
