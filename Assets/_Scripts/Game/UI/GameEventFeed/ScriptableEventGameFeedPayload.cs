using CosmicShore.Game.Arcade;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Game.UI.Animations;
using CosmicShore.Game.UI.NotificationSystem.Payload;
using CosmicShore.Game.UI.PreGameCinematic;
using CosmicShore.MinigameHUD.View;
namespace CosmicShore.Game.UI.GameEventFeed
{
    [CreateAssetMenu(
        fileName = "Event_GameFeedPayload",
        menuName = "ScriptableObjects/SOAP/Events/GameFeedPayload")]
    public class ScriptableEventGameFeedPayload : ScriptableEvent<GameFeedPayload> { }
}
