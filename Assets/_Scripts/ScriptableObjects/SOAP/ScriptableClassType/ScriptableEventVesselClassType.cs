using UnityEngine;
using Obvious.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(VesselClassType), menuName = "ScriptableObjects/Events/"+ nameof(VesselClassType))]
    public class ScriptableEventShipClassType : ScriptableEvent<VesselClassType>
    {
    
    }
}
