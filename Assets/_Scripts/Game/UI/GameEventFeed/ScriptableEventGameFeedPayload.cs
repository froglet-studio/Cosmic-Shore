using CosmicShore.Game.Arcade;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI;
using CosmicShore.MinigameHUD.View;
namespace CosmicShore.Game.UI
{
    [CreateAssetMenu(
        fileName = "Event_GameFeedPayload",
        menuName = "ScriptableObjects/SOAP/Events/GameFeedPayload")]
    public class ScriptableEventGameFeedPayload : ScriptableEvent<GameFeedPayload> { }
}
