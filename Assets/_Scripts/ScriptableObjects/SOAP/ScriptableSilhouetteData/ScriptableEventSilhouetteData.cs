using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(SilhouetteData), menuName = "ScriptableObjects/Events/"+ nameof(SilhouetteData))]
    public class ScriptableEventSilhouetteData : ScriptableEvent<SilhouetteData>
    {
        
    }
}
