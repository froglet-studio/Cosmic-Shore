using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Soap
{
    [CreateAssetMenu(
        fileName = "scriptable_event_" + nameof(BenchmarkStateData),
        menuName = "Soap/ScriptableEvents/" + nameof(BenchmarkStateData))]
    public class ScriptableEventBenchmarkStateData : ScriptableEvent<BenchmarkStateData>
    {
    }
}
