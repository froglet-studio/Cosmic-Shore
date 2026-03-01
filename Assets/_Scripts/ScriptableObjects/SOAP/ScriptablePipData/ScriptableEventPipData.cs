using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(PipData), menuName = "ScriptableObjects/Events/" + nameof(PipData))]
    public class ScriptableEventPipData : ScriptableEvent<PipData>
    {

    }
}
