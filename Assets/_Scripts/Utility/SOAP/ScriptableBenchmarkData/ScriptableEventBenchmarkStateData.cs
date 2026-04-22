using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Utility
{
    [CreateAssetMenu(
        fileName = "scriptable_event_" + nameof(BenchmarkStateData),
        menuName = "ScriptableObjects/Events/" + nameof(BenchmarkStateData))]
    public class ScriptableEventBenchmarkStateData : ScriptableEvent<BenchmarkStateData>
    {
    }
}
