using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.SOAP
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(MiniGameDataSO), menuName = "Soap/ScriptableEvents/"+ nameof(MiniGameDataSO))]
    public class ScriptableEventMiniGameData : ScriptableEvent<MiniGameDataSO>
    {
        
    }
}
