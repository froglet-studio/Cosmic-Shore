// PORT Deviation — log-only SHELL of ToastNotificationAPI (original: Assets/_Scripts/UI/
// ToastNotification/ToastNotificationAPI.cs — a static facade that auto-creates the
// ToastNotificationManager singleton, wires channel/settings from Resources, and
// discovers the scene container by name, including inactive objects). Landed as a shell
// in Arc F 2b-iii(b): the only port-side caller (ArcadeGameConfigureModal.
// HandleLockedIntensitySelected) is unreachable while GameModeProgressionService is a
// shell (GetQuestForMode returns null), so the shell equals shipping behavior today.
// The toast-notification subsystem (manager + channel + settings + views) is its own
// future unit.
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    /// <summary>
    /// Static convenience API for showing toast notifications from anywhere in the codebase.
    /// </summary>
    public static class ToastNotificationAPI
    {
        /// <summary>Show a toast notification with the given message.</summary>
        public static void Show(string message)
        {
            CSDebug.Log($"[ToastNotificationAPI] (shell) {message}");
        }
    }
}
