using CosmicShore.Game.Managers;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptableCrystalStats
{
    [CreateAssetMenu(fileName = "Event_" + nameof(CrystalStats), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(CrystalStats))]
    public class ScriptableEventCrystalStats : ScriptableEvent<CrystalStats>
    {
        
    }
}
