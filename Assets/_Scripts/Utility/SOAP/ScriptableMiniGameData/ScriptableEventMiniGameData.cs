using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.SOAP
{
    [CreateAssetMenu(fileName = "scriptable_event_" + nameof(MiniGameData), menuName = "Soap/ScriptableEvents/"+ nameof(MiniGameData))]
    public class ScriptableEventMiniGameData : ScriptableEvent<MiniGameData>
    {
        
    }
}
