using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.App.UI.ToastNotification
{
    /// <summary>
    /// Static convenience API for showing toast notifications from anywhere in the codebase.
    /// Auto-creates the ToastNotificationManager singleton if it doesn't exist in the scene.
    /// </summary>
    public static class ToastNotificationAPI
    {
        private const string ChannelPath = "Channels/ToastNotificationChannel";
        private const string SettingsPath = "ToastNotificationSettings";

        private static ToastNotificationChannel _channel;

        private static ToastNotificationChannel Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ToastNotificationChannel>(ChannelPath));

        /// <summary>
        /// Show a toast notification with the given message.
        /// Ensures the manager exists before dispatching.
        /// </summary>
        public static void Show(string message)
        {
            EnsureManagerExists();

            // Prefer direct call since we just ensured the manager exists
            if (ToastNotificationManager.Instance != null)
            {
                ToastNotificationManager.Instance.Show(message);
                return;
            }

            CSDebug.LogWarning(
                $"[ToastNotificationAPI] Manager creation failed. Message dropped: {message}");
        }

        private static void EnsureManagerExists()
        {
            if (ToastNotificationManager.Instance != null) return;

            var go = new GameObject("ToastNotificationManager");
            var mgr = go.AddComponent<ToastNotificationManager>();

            // Wire settings from Resources
            var settings = Resources.Load<ToastNotificationSettingsSO>(SettingsPath);
            if (settings != null)
            {
                var field = typeof(ToastNotificationManager).GetField("settings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(mgr, settings);
            }

            // Wire channel
            var channel = Channel;
            if (channel != null)
            {
                var field = typeof(ToastNotificationManager).GetField("channel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(mgr, channel);

                // Re-subscribe since OnEnable ran before channel was wired
                mgr.enabled = false;
                mgr.enabled = true;
            }

            CSDebug.Log("[ToastNotificationAPI] Auto-created ToastNotificationManager.");
        }
    }
}
