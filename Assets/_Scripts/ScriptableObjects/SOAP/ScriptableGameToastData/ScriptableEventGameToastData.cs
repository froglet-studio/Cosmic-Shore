using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(GameToastData),
        menuName = "ScriptableObjects/Events/" + nameof(GameToastData))]
    public class ScriptableEventGameToastData : ScriptableEvent<GameToastData>
    {
    }
}
