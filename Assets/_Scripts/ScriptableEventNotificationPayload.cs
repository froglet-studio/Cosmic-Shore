using CosmicShore.Game.UI;
using UnityEngine;
using Obvious.Soap;

[CreateAssetMenu(
    fileName = "scriptable_event_NotificationPayload" + nameof(NotificationPayload),
    menuName = "Soap/ScriptableEvents/NotificationPayload")]
public class ScriptableEventNotificationPayload : ScriptableEvent<NotificationPayload> { }