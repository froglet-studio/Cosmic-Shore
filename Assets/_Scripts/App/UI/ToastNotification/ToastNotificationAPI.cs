using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.App.UI.ToastNotification
{
    /// <summary>
    /// Static convenience API for showing toast notifications from anywhere in the codebase.
    /// Works via either the SOAP channel (decoupled) or the singleton manager (direct).
    /// </summary>
    public static class ToastNotificationAPI
    {
        private const string ChannelPath = "Channels/ToastNotificationChannel";

        private static ToastNotificationChannel _channel;

        private static ToastNotificationChannel Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ToastNotificationChannel>(ChannelPath));

        /// <summary>
        /// Show a toast notification with the given message.
        /// Prefers the SOAP channel if available, falls back to the singleton.
        /// </summary>
        public static void Show(string message)
        {
            var ch = Channel;
            if (ch != null)
            {
                ch.Raise(message);
                return;
            }

            // Fallback: call manager directly
            if (ToastNotificationManager.Instance != null)
            {
                ToastNotificationManager.Instance.Show(message);
                return;
            }

            CSDebug.LogWarning(
                $"[ToastNotificationAPI] No channel at Resources/{ChannelPath} and no manager instance. " +
                $"Message dropped: {message}");
        }
    }
}
