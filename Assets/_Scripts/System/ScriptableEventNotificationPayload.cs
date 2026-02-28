using CosmicShore.UI;
using UnityEngine;
using Obvious.Soap;

namespace CosmicShore.Core
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(NotificationPayload),
        menuName = "ScriptableObjects/SOAP/Events/NotificationPayload")]
    public class ScriptableEventNotificationPayload : ScriptableEvent<NotificationPayload> { }
}
