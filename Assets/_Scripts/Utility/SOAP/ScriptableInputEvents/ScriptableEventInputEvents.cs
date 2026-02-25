using UnityEngine;
using Obvious.Soap;
using CosmicShore.Models.Enums;

namespace CosmicShore.Utility.SOAP.ScriptableInputEvents
{
    [CreateAssetMenu(fileName = "Event_" + nameof(InputEvents), menuName = "ScriptableObjects/SOAP/Events/"+ nameof(InputEvents))]
    public class ScriptableEventInputEvents : ScriptableEvent<InputEvents>
    {
        
    }
}
