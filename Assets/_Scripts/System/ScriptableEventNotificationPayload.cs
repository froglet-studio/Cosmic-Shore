using CosmicShore.UI;
using UnityEngine;
using CosmicShore.Soap;

namespace CosmicShore.Core
{
    [CreateAssetMenu(
        fileName = "Event_" + nameof(NotificationPayload),
        menuName = "ScriptableObjects/Events/NotificationPayload")]
    public class ScriptableEventNotificationPayload : ScriptableEvent<NotificationPayload> { }
}
