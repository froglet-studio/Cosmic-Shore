using CosmicShore.Utility;

namespace CosmicShore.UI
{
    /// <summary>
    /// Static convenience API for showing toast notifications from anywhere in the codebase.
    ///
    /// <para>The heavy lifting lives in <see cref="ToastNotificationManager"/>, which boots
    /// itself before the first scene, loads its settings + SOAP channel from <c>Resources</c>,
    /// and owns a persistent overlay canvas so toasts render in every scene. This class is a
    /// thin, allocation-free facade over it.</para>
    ///
    /// <para>Main-thread only. Off-thread callers (UGS / Netcode continuations) must marshal
    /// via <c>.AsMainThread()</c> before calling — see <c>Docs/THREADING.md</c>.</para>
    /// </summary>
    public static class ToastNotificationAPI
    {
        /// <summary>Show a toast notification with the given message.</summary>
        public static void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            var manager = ToastNotificationManager.EnsureInstance();
            if (manager != null)
            {
                manager.Show(message);
                return;
            }

            CSDebug.LogWarning($"[ToastNotificationAPI] Manager unavailable. Message dropped: {message}");
        }
    }
}
