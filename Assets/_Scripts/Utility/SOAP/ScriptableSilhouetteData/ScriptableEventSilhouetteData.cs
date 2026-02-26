using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP
{
    [CreateAssetMenu(fileName = "Event_" + nameof(SilhouetteData), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(SilhouetteData))]
    public class ScriptableEventSilhouetteData : ScriptableEvent<SilhouetteData>
    {
        
    }
}
