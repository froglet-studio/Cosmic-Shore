using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility.SOAP.ScriptablePipData
{
    [CreateAssetMenu(fileName = "Event_" + nameof(PipData), menuName = "ScriptableObjects/SOAP/Events/" + nameof(PipData))]
    public class ScriptableEventPipData : ScriptableEvent<PipData>
    {

    }
}
