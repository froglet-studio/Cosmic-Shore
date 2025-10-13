using UnityEngine;

namespace CosmicShore.Game.UI
{
    public static class NotificationAPI
    {
        private const string ChannelPath = "Channels/NotificationChannel";

        // Cached SOAP event channel (NotificationPayload)
        private static ScriptableEventNotificationPayload _channel;

        private static ScriptableEventNotificationPayload Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ScriptableEventNotificationPayload>(ChannelPath));

        /// <summary>Notify using header + title.</summary>
        public static void Notify(string header, string title)
        {
            var ch = Channel;
            if (ch == null)
            {
                Debug.LogWarning($"[NotificationAPI] Missing channel at Resources/{ChannelPath}");
                return;
            }
            ch.Raise(new NotificationPayload(header, title));
        }

        /// <summary>Notify with a prebuilt payload.</summary>
        public static void Notify(NotificationPayload payload)
        {
            var ch = Channel;
            if (ch == null)
            {
                Debug.LogWarning($"[NotificationAPI] Missing channel at Resources/{ChannelPath}");
                return;
            }
            ch.Raise(payload);
        }
    }
}