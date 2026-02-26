using Obvious.Soap;
using UnityEngine;
using CosmicShore.Game.ImpactEffects;
namespace CosmicShore.Utility.SOAP
{
    [CreateAssetMenu(
        fileName = "Event_VesselImpactor",
        menuName = "ScriptableObjects/SOAP/Events/VesselImpactor")]
    public class ScriptableEventVesselImpactor : ScriptableEvent<VesselImpactor>
    {
    }
}