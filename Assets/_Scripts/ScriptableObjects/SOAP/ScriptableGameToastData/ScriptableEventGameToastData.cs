using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(GameToastData),
        menuName = "ScriptableObjects/Events/" + nameof(GameToastData))]
    public class ScriptableEventGameToastData : ScriptableEvent<GameToastData>
    {
    }
}
