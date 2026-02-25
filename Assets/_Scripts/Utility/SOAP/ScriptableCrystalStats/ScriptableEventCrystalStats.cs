using CosmicShore.Core;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utilities
{
    [CreateAssetMenu(fileName = "Event_" + nameof(CrystalStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(CrystalStats))]
    public class ScriptableEventCrystalStats : ScriptableEvent<CrystalStats>
    {
        
    }
}
