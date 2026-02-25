using UnityEngine;
using Obvious.Soap;
using CosmicShore.Models.Enums;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(fileName = "Event_" + nameof(InputEvents), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(InputEvents))]
    public class ScriptableEventInputEvents : ScriptableEvent<InputEvents>
    {
        
    }
}
