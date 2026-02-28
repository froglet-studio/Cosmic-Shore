using CosmicShore.Gameplay;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(CrystalStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(CrystalStats))]
    public class ScriptableEventCrystalStats : ScriptableEvent<CrystalStats>
    {
        
    }
}
