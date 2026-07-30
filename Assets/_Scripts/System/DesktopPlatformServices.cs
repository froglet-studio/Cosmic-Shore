using System;
using System.IO;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Desktop-side behaviour for things the mobile build gets from the OS.
    ///
    /// Cosmic Shore was built mobile-first, so several player-facing actions route through
    /// NativeShare - which has no implementation on Windows/macOS/Linux and silently does nothing
    /// there. On a Steam build that reads as a dead button. These helpers give the desktop player
    /// the equivalent outcome (a mail client, a folder with the file in it) and leave the mobile
    /// path untouched.
    ///
    /// Also owns the quit path: a PC game must be closable from its own UI, and
    /// <c>Application.Quit</c> appeared nowhere in the project before this.
    /// </summary>
    public static class DesktopPlatformServices
    {
        /// <summary>
        /// True on standalone desktop players and in the editor - i.e. anywhere NativeShare's
        /// share sheet does not exist and a fallback is required.
        /// </summary>
        public static bool IsDesktop =>
            Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.LinuxPlayer ||
            Application.isEditor;

        // ──────────────────────────────────────────────
        //  Quit
        // ──────────────────────────────────────────────

        /// <summary>
        /// Closes the game. Raises Unity's normal quit path, so
        /// <c>ApplicationLifecycleManager.OnApplicationQuit</c> still fires and the analytics
        /// facade still gets its final flush. Stops play mode instead when run from the editor.
        /// </summary>
        public static void Quit()
        {
            Debug.Log("[Platform] Quit requested.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ──────────────────────────────────────────────
        //  Email
        // ──────────────────────────────────────────────

        /// <summary>
        /// Opens the player's mail client with the message pre-filled. Desktop counterpart to
        /// NativeShare's email path. Returns false if the URL could not be opened, so the caller
        /// can surface a copyable address instead of failing silently.
        /// </summary>
        public static bool TryOpenMailClient(string recipient, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(recipient)) return false;

            try
            {
                string url = $"mailto:{Uri.EscapeDataString(recipient)}" +
                             $"?subject={Uri.EscapeDataString(subject ?? string.Empty)}" +
                             $"&body={Uri.EscapeDataString(body ?? string.Empty)}";

                Application.OpenURL(url);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Platform] Could not open mail client: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────
        //  Files
        // ──────────────────────────────────────────────

        /// <summary>
        /// Where desktop players expect to find things the game produced for them. Under
        /// persistentDataPath so it survives updates and needs no extra permissions.
        /// </summary>
        public static string ShareFolder
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "Shared");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// Copies a produced file into the share folder and opens that folder, which is the desktop
        /// equivalent of handing it to a share sheet. Returns the final path, or null on failure.
        /// </summary>
        public static string SaveAndReveal(string sourcePath, string friendlyFileName = null)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                Debug.LogWarning($"[Platform] Nothing to reveal at '{sourcePath}'.");
                return null;
            }

            try
            {
                string fileName = string.IsNullOrWhiteSpace(friendlyFileName)
                    ? Path.GetFileName(sourcePath)
                    : friendlyFileName;

                string destination = Path.Combine(ShareFolder, fileName);
                File.Copy(sourcePath, destination, overwrite: true);

                Application.OpenURL("file://" + ShareFolder.Replace('\\', '/'));
                Debug.Log($"[Platform] Saved to {destination}");
                return destination;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Platform] Could not save/reveal file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Timestamped file name so repeated shares in one session never overwrite each other.
        /// Callers pass their own prefix, e.g. "cosmic-shore-score".
        /// </summary>
        public static string TimestampedName(string prefix, string extension) =>
            $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension.TrimStart('.')}";
    }
}
