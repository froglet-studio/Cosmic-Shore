using Obvious.Soap;
using UnityEngine;
using CosmicShore.Game.ImpactEffects.Impactors;
namespace CosmicShore.Utility.SOAP.ScriptableClassType
{
    [CreateAssetMenu(
        fileName = "Event_VesselImpactor",
        menuName = "ScriptableObjects/SOAP/Events/VesselImpactor")]
    public class ScriptableEventVesselImpactor : ScriptableEvent<VesselImpactor>
    {
    }
}