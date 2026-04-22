using CosmicShore.Gameplay;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.UI;
namespace CosmicShore.UI
{
    [CreateAssetMenu(
        fileName = "Event_GameFeedPayload",
        menuName = "ScriptableObjects/Events/GameFeedPayload")]
    public class ScriptableEventGameFeedPayload : ScriptableEvent<GameFeedPayload> { }
}
