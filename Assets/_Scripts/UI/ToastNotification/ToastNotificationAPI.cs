using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Static convenience API for showing toast notifications from anywhere in the codebase.
    /// Auto-creates the ToastNotificationManager singleton if it doesn't exist in the scene.
    /// Finds the container by searching for a GameObject named "ToastNotificationContainer".
    /// </summary>
    public static class ToastNotificationAPI
    {
        private const string ChannelPath = "Channels/ToastNotificationChannel";
        private const string SettingsPath = "ToastNotificationSettings";
        private const string ContainerName = "ToastNotificationContainer";

        private static ToastNotificationChannel _channel;

        private static ToastNotificationChannel Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ToastNotificationChannel>(ChannelPath));

        /// <summary>
        /// Show a toast notification with the given message.
        /// Ensures the manager and container exist before dispatching.
        /// </summary>
        public static void Show(string message)
        {
            EnsureManagerExists();

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
            if (ToastNotificationManager.Instance != null)
            {
                // Manager exists but container may have been destroyed (scene change)
                if (ToastNotificationManager.Instance.Container == null)
                    TryAssignContainer(ToastNotificationManager.Instance);
                return;
            }

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

                mgr.enabled = false;
                mgr.enabled = true;
            }

            TryAssignContainer(mgr);

            CSDebug.Log("[ToastNotificationAPI] Auto-created ToastNotificationManager.");
        }

        private static void TryAssignContainer(ToastNotificationManager mgr)
        {
            var containerGO = GameObject.Find(ContainerName);
            if (containerGO != null)
            {
                var rt = containerGO.GetComponent<RectTransform>();
                if (rt != null)
                    mgr.Container = rt;
            }
            else
            {
                CSDebug.LogWarning(
                    $"[ToastNotificationAPI] No GameObject named '{ContainerName}' found in scene. " +
                    "Toasts will not display until a container is available.");
            }
        }
    }
}
