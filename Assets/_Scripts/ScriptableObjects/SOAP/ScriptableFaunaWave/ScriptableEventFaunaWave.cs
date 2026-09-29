using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(FaunaWaveData),
        menuName = "ScriptableObjects/Events/" + nameof(FaunaWaveData))]
    public class ScriptableEventFaunaWave : ScriptableEvent<FaunaWaveData>
    {
    }
}
