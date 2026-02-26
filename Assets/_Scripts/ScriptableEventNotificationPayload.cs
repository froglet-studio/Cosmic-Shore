using CosmicShore.Game.UI;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(NotificationPayload),
        menuName = "ScriptableObjects/SOAP/Events/NotificationPayload")]
    public class ScriptableEventNotificationPayload : ScriptableEvent<NotificationPayload> { }
}
