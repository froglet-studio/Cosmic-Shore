using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(
        fileName = "VesselImpactorEvent",
        menuName = "ScriptableObjects/Events/Vessel/VesselImpactorEvent")]
    public class ScriptableEventVesselImpactor : ScriptableEvent<VesselImpactor>
    {
    }
}