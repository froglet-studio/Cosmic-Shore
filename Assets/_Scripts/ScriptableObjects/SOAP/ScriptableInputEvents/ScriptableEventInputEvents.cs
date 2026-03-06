using UnityEngine;
using Obvious.Soap;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(InputEvents), menuName = "ScriptableObjects/Events/"+ nameof(InputEvents))]
    public class ScriptableEventInputEvents : ScriptableEvent<InputEvents>
    {
        
    }
}
