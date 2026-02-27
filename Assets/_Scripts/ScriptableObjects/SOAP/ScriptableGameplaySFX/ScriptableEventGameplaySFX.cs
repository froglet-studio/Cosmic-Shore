using UnityEngine;
using Obvious.Soap;
using CosmicShore.Core;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Event_" + nameof(GameplaySFXCategory), menuName = "ScriptableObjects/SOAP/Events/" + nameof(GameplaySFXCategory))]
    public class ScriptableEventGameplaySFX : ScriptableEvent<GameplaySFXCategory>
    {
    }
}
